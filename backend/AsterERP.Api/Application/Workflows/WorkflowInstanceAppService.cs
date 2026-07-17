using System.Text;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Workflows.Callbacks;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using CoreIdentityLinkEntity = AsterERP.Workflow.Core.Cmd.IdentityLinkEntity;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowInstanceAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IWorkflowCurrentUserContext currentUserContext,
    IRuntimeService runtimeService,
    ITaskService taskService,
    IHistoryService historyService,
    IRepositoryService repositoryService,
    IRuntimeDataModelService runtimeDataModelService,
    IWorkflowFormResourceAppService formResourceService,
    IWorkflowNotificationAppService notificationService,
    WorkflowCallbackExecutor callbackExecutor,
    IWorkflowIdentityDisplayService identityDisplayService,
    WorkflowParticipantVariableResolver participantVariableResolver,
    IClock clock,
    IUnitOfWorkManager unitOfWorkManager) : IWorkflowInstanceAppService
{
    public async Task<GridPageResult<WorkflowInstanceListItemResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        return await QueryInstancesAsync(query, startedBy: null, cancellationToken);
    }

    public async Task<GridPageResult<WorkflowInstanceListItemResponse>> GetMineAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        return await QueryInstancesAsync(query, currentUserContext.UserId, cancellationToken);
    }

    public async Task<WorkflowInstanceResponse> StartAsync(WorkflowStartInstanceRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = Normalize(request.TenantId, currentUserContext.TenantId, "租户不能为空");
        var appCode = Normalize(request.AppCode, currentUserContext.AppCode, "应用不能为空").ToUpperInvariant();
        var menuCode = Normalize(request.MenuCode, null, "菜单编码不能为空");
        var businessType = Normalize(request.BusinessType, null, "业务类型不能为空");
        var businessKey = Normalize(request.BusinessKey, null, "业务主键不能为空");

        var binding = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.IsEnabled &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.MenuCode == menuCode &&
                item.BusinessType == businessType,
                cancellationToken)
            ?? throw new NotFoundException("未找到可用审批流绑定", ErrorCodes.WorkflowBindingNotFound);

        var businessVariables = await ResolveBusinessVariablesAsync(binding, businessKey, cancellationToken);
        var submittedVariables = MergeSubmittedVariables(businessVariables, request.Variables);
        var variables = new Dictionary<string, object?>(submittedVariables, StringComparer.OrdinalIgnoreCase)
        {
            ["tenantId"] = tenantId,
            ["appCode"] = appCode,
            ["menuCode"] = menuCode,
            ["businessType"] = businessType,
            ["businessKey"] = businessKey,
            ["starterUserId"] = currentUserContext.UserId,
            ["starterUserName"] = currentUserContext.UserName
        };
        await participantVariableResolver.EnrichStartVariablesAsync(variables, cancellationToken);
        await participantVariableResolver.EnrichProcessDefinitionVariablesAsync(
            binding.ProcessDefinitionId,
            binding.ProcessDefinitionKey,
            variables,
            cancellationToken);
        await participantVariableResolver.ValidateRequiredVariablesAsync(
            binding.ProcessDefinitionId,
            binding.ProcessDefinitionKey,
            variables,
            cancellationToken);

        string? processInstanceId = null;
        try
        {
            processInstanceId = string.IsNullOrWhiteSpace(binding.ProcessDefinitionId)
                ? await runtimeService.StartProcessInstanceByKeyAsync(binding.ProcessDefinitionKey, businessKey, variables, cancellationToken)
                : await runtimeService.StartProcessInstanceByIdAsync(binding.ProcessDefinitionId, businessKey, variables, cancellationToken);

            await runtimeService.AddUserIdentityLinkAsync(processInstanceId, currentUserContext.UserId, "starter", cancellationToken);
            await ExecuteInWorkflowTransactionAsync(async () =>
            {
                var runtimeExecution = await databaseAccessor.GetCurrentDb().Queryable<ExecutionEntity>()
                    .FirstAsync(item => item.Id == processInstanceId || item.ProcessInstanceId == processInstanceId, cancellationToken);
                var processDefinitionId = runtimeExecution?.ProcessDefinitionId ?? binding.ProcessDefinitionId;

                var instance = new WorkflowBusinessInstanceEntity
                {
                    TenantId = tenantId,
                    AppCode = appCode,
                    MenuCode = menuCode,
                    BusinessType = businessType,
                    BusinessKey = businessKey,
                    ProcessInstanceId = processInstanceId,
                    ProcessDefinitionId = processDefinitionId,
                    ProcessDefinitionKey = binding.ProcessDefinitionKey,
                    Status = "Running",
                    StartedBy = currentUserContext.UserId,
                    StartedAt = clock.Now,
                    VariableSnapshotJson = WorkflowJson.Serialize(variables),
                    SubmittedFormJson = WorkflowSubmittedFormSnapshot.Capture(submittedVariables),
                    Remark = request.Title
                };
                await databaseAccessor.GetCurrentDb().Insertable(instance).ExecuteCommandAsync(cancellationToken);
                await callbackExecutor.ExecuteAsync(
                    BuildCallbackContext(instance, WorkflowCallbackTriggers.ProcessStart, null, null, null, variables),
                    cancellationToken);
                await QueueInstanceNotificationAsync(instance, "process-start", null, cancellationToken);
                await QueueRuntimeTaskNotificationsAsync(instance, "node-enter", cancellationToken);
            }, cancellationToken);

            return await GetDetailAsync(processInstanceId, cancellationToken);
        }
        catch
        {
            await TryDeleteStartedProcessAsync(processInstanceId, cancellationToken);
            throw;
        }
    }

    public async Task<WorkflowInstanceResponse> GetDetailAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await GetBusinessInstanceAsync(processInstanceId, cancellationToken);
        var variables = await ResolveRuntimeVariablesAsync(processInstanceId, cancellationToken);
        var tasks = await taskService.GetTasksByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        var taskResponses = await MapTasksAsync(tasks, cancellationToken);
        var activities = await GetActivityResponsesAsync(processInstanceId, cancellationToken);
        var comments = (await taskService.GetProcessInstanceCommentsAsync(processInstanceId, null, cancellationToken))
            .Select(MapComment)
            .OrderBy(item => item.Time)
            .ToList();
        var attachments = (await taskService.GetProcessInstanceAttachmentsAsync(processInstanceId, cancellationToken))
            .Select(MapAttachment)
            .OrderBy(item => item.Id)
            .ToList();
        var identityLinks = await GetIdentityLinksAsync(processInstanceId, cancellationToken);
        var starterNames = await identityDisplayService.GetUserDisplayNamesAsync([instance.StartedBy], cancellationToken);
        var timeline = await BuildTimelineAsync(instance, activities, comments, attachments, identityLinks, cancellationToken);
        var notifications = await notificationService.GetInstanceNotificationsAsync(processInstanceId, cancellationToken);
        var submittedFormLabels = await formResourceService.GetFieldLabelsForBindingAsync(
            instance.TenantId,
            instance.AppCode,
            instance.MenuCode,
            instance.BusinessType,
            cancellationToken);
        var submittedForm = WorkflowSubmittedFormSnapshot.Build(
            instance.SubmittedFormJson,
            instance.VariableSnapshotJson,
            submittedFormLabels);

        return new WorkflowInstanceResponse(
            instance.Id,
            instance.TenantId,
            instance.AppCode,
            instance.MenuCode,
            instance.BusinessType,
            instance.BusinessKey,
            instance.ProcessInstanceId,
            instance.ProcessDefinitionId,
            instance.ProcessDefinitionKey,
            instance.Status,
            instance.StartedBy,
            instance.StartedAt,
            instance.FinishedAt,
            variables.Count == 0 ? WorkflowJson.DeserializeVariables(instance.VariableSnapshotJson) : variables,
            taskResponses,
            activities)
        {
            StartedByName = identityDisplayService.ResolveUserName(instance.StartedBy, starterNames),
            Timeline = timeline,
            Comments = comments,
            Attachments = attachments,
            IdentityLinks = identityLinks,
            Notifications = notifications,
            SubmittedForm = submittedForm
        };
    }

    public async Task WithdrawAsync(string processInstanceId, string? reason, CancellationToken cancellationToken = default)
    {
        await ExecuteInWorkflowTransactionAsync(
            () => DeleteAndMarkAsync(processInstanceId, "Withdrawn", reason ?? "withdrawn by starter", cancellationToken),
            cancellationToken);
    }

    public async Task TerminateAsync(string processInstanceId, string? reason, CancellationToken cancellationToken = default)
    {
        await ExecuteInWorkflowTransactionAsync(
            () => DeleteAndMarkAsync(processInstanceId, "Terminated", reason ?? "terminated", cancellationToken),
            cancellationToken);
    }

    public async Task<WorkflowInstanceResponse> SetVariablesAsync(string processInstanceId, WorkflowInstanceVariableRequest request, CancellationToken cancellationToken = default)
    {
        var executionId = await ResolveRootExecutionIdAsync(processInstanceId, cancellationToken);
        await runtimeService.SetVariablesAsync(executionId, request.Variables, cancellationToken);

        var instance = await GetBusinessInstanceAsync(processInstanceId, cancellationToken);
        var snapshot = WorkflowJson.DeserializeVariables(instance.VariableSnapshotJson);
        foreach (var variable in request.Variables)
        {
            snapshot[variable.Key] = variable.Value;
        }
        instance.VariableSnapshotJson = WorkflowJson.Serialize(snapshot);
        instance.UpdatedBy = currentUserContext.UserId;
        instance.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(instance).ExecuteCommandAsync(cancellationToken);

        return await GetDetailAsync(processInstanceId, cancellationToken);
    }

    public async Task<WorkflowHighlightedDiagramResponse> GetHighlightedDiagramAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var instance = await GetBusinessInstanceAsync(processInstanceId, cancellationToken);
        var processDefinitionId = await ResolveProcessDefinitionIdAsync(instance, cancellationToken);
        var bpmnBytes = processDefinitionId is null
            ? null
            : await GetProcessDefinitionResourceAsync(processDefinitionId, cancellationToken);
        var activeIds = await ResolveActiveActivityIdsAsync(processInstanceId, cancellationToken);
        var completedIds = await databaseAccessor.GetCurrentDb().Queryable<HistoricActivityInstanceEntity>()
            .Where(item => item.ProcessInstanceId == processInstanceId && item.EndTime != null)
            .Select(item => item.ActivityId)
            .ToListAsync(cancellationToken);

        return new WorkflowHighlightedDiagramResponse(
            processInstanceId,
            bpmnBytes is null ? string.Empty : Encoding.UTF8.GetString(bpmnBytes),
            activeIds,
            completedIds.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct(StringComparer.Ordinal).ToList());
    }

    private async Task<byte[]?> GetProcessDefinitionResourceAsync(string processDefinitionId, CancellationToken cancellationToken)
    {
        var processDefinition = await databaseAccessor.GetCurrentDb().Queryable<ProcessDefinitionEntity>()
            .FirstAsync(item => item.Id == processDefinitionId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(processDefinition?.DeploymentId) &&
            !string.IsNullOrWhiteSpace(processDefinition.ResourceName))
        {
            return await repositoryService.GetResourceAsync(
                processDefinition.DeploymentId,
                processDefinition.ResourceName,
                cancellationToken);
        }

        return await repositoryService.GetProcessModelAsync(processDefinitionId, cancellationToken);
    }

    public async Task SignalAsync(string executionId, WorkflowInstanceVariableRequest? request, CancellationToken cancellationToken = default)
    {
        await runtimeService.SignalAsync(executionId, request?.Variables, cancellationToken);
    }

    public async Task MessageAsync(string executionId, string messageName, WorkflowInstanceVariableRequest? request, CancellationToken cancellationToken = default)
    {
        await runtimeService.MessageEventReceivedAsync(messageName, executionId, request?.Variables, cancellationToken);
    }

    internal async Task<bool> MarkCompletedIfEndedAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        var hasRuntimeTask = await databaseAccessor.GetCurrentDb().Queryable<TaskEntity>()
            .Where(item => item.ProcessInstanceId == processInstanceId)
            .AnyAsync(cancellationToken);
        var hasActiveExecution = await databaseAccessor.GetCurrentDb().Queryable<ExecutionEntity>()
            .Where(item => (item.Id == processInstanceId || item.ProcessInstanceId == processInstanceId) && item.IsActive && !item.IsEnded)
            .AnyAsync(cancellationToken);
        if (hasRuntimeTask || hasActiveExecution)
        {
            return false;
        }

        var instance = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
            .FirstAsync(item => item.ProcessInstanceId == processInstanceId && !item.IsDeleted, cancellationToken);
        if (instance is null || instance.Status == "Completed")
        {
            return false;
        }

        instance.Status = "Completed";
        instance.FinishedAt = clock.Now;
        instance.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(instance).ExecuteCommandAsync(cancellationToken);
        await callbackExecutor.ExecuteAsync(
            BuildCallbackContext(instance, WorkflowCallbackTriggers.ProcessCompleted, null, null, null, null),
            cancellationToken);
        await QueueInstanceNotificationAsync(instance, "process-end", null, cancellationToken);
        return true;
    }

    internal async Task QueueRuntimeTaskNotificationsAsync(
        WorkflowBusinessInstanceEntity instance,
        string trigger,
        CancellationToken cancellationToken)
    {
        var tasks = await taskService.GetTasksByProcessInstanceIdAsync(instance.ProcessInstanceId, cancellationToken);
        foreach (var task in tasks)
        {
            await notificationService.QueueAsync(
                new WorkflowNotificationTriggerContext(
                    instance.TenantId,
                    instance.AppCode,
                    null,
                    task.ProcessDefinitionId ?? instance.ProcessDefinitionId,
                    instance.ProcessDefinitionKey,
                    instance.ProcessInstanceId,
                    task.Id,
                    task.TaskDefinitionKey,
                    trigger,
                    instance.StartedBy,
                    currentUserContext.UserId,
                    WorkflowJson.DeserializeVariables(instance.VariableSnapshotJson)),
                cancellationToken);
        }
    }

    internal Task QueueInstanceNotificationAsync(
        WorkflowBusinessInstanceEntity instance,
        string trigger,
        string? nodeId,
        CancellationToken cancellationToken)
    {
        return notificationService.QueueAsync(
            new WorkflowNotificationTriggerContext(
                instance.TenantId,
                instance.AppCode,
                null,
                instance.ProcessDefinitionId,
                instance.ProcessDefinitionKey,
                instance.ProcessInstanceId,
                null,
                nodeId,
                trigger,
                instance.StartedBy,
                currentUserContext.UserId,
                WorkflowJson.DeserializeVariables(instance.VariableSnapshotJson)),
            cancellationToken);
    }

    private async Task TryDeleteStartedProcessAsync(string? processInstanceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
        {
            return;
        }

        try
        {
            await runtimeService.DeleteProcessInstanceAsync(processInstanceId, "start compensation after business callback failure", cancellationToken);
        }
        catch
        {
            // Preserve the original start failure. The failed compensation can be diagnosed from engine/runtime state.
        }
    }

    private async Task<GridPageResult<WorkflowInstanceListItemResponse>> QueryInstancesAsync(
        GridQuery query,
        string? startedBy,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
            .Where(item => !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(query.TenantId), item => item.TenantId == query.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.AppCode), item => item.AppCode == query.AppCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status), item => item.Status == query.Status)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.BusinessKey.Contains(query.Keyword!) || item.BusinessType.Contains(query.Keyword!) || item.ProcessDefinitionKey.Contains(query.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(startedBy), item => item.StartedBy == startedBy)
            .OrderBy(item => item.StartedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return new GridPageResult<WorkflowInstanceListItemResponse>
        {
            Total = total.Value,
            Items = items.Select(MapListItem).ToList()
        };
    }

    private async Task DeleteAndMarkAsync(string processInstanceId, string status, string reason, CancellationToken cancellationToken)
    {
        var instance = await GetBusinessInstanceAsync(processInstanceId, cancellationToken);
        await runtimeService.DeleteProcessInstanceAsync(processInstanceId, reason, cancellationToken);
        instance.Status = status;
        instance.FinishedAt = clock.Now;
        instance.UpdatedBy = currentUserContext.UserId;
        instance.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(instance).ExecuteCommandAsync(cancellationToken);
        await callbackExecutor.ExecuteAsync(
            BuildCallbackContext(instance, ResolveTerminalCallbackTrigger(status), null, null, null, null),
            cancellationToken);
    }

    private WorkflowCallbackContext BuildCallbackContext(
        WorkflowBusinessInstanceEntity instance,
        string trigger,
        string? nodeId,
        string? workflowTaskId,
        string? action,
        IReadOnlyDictionary<string, object?>? variables)
    {
        var mergedVariables = WorkflowJson.DeserializeVariables(instance.VariableSnapshotJson);
        foreach (var variable in variables ?? new Dictionary<string, object?>())
        {
            mergedVariables[variable.Key] = variable.Value;
        }

        return new WorkflowCallbackContext(
            instance,
            trigger,
            nodeId,
            workflowTaskId,
            action,
            currentUserContext.UserId,
            mergedVariables,
            clock.Now);
    }

    private static string ResolveTerminalCallbackTrigger(string status)
    {
        return status switch
        {
            "Withdrawn" => WorkflowCallbackTriggers.ProcessWithdrawn,
            "Terminated" => WorkflowCallbackTriggers.ProcessTerminated,
            _ => WorkflowCallbackTriggers.ProcessCompleted
        };
    }

    private async Task ExecuteInWorkflowTransactionAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        await ExecuteInWorkflowTransactionAsync(
            async () =>
            {
                await action();
                return true;
            },
            cancellationToken);
    }

    private async Task<T> ExecuteInWorkflowTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        using var unitOfWork = unitOfWorkManager.Begin();
        var result = await action();
        await unitOfWork.CompleteAsync(cancellationToken);
        return result;
    }

    private async Task<WorkflowBusinessInstanceEntity> GetBusinessInstanceAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
            .FirstAsync(item => item.ProcessInstanceId == processInstanceId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("流程实例不存在", ErrorCodes.WorkflowInstanceNotFound);
    }

    private async Task<string> ResolveRootExecutionIdAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        var execution = await databaseAccessor.GetCurrentDb().Queryable<ExecutionEntity>()
            .FirstAsync(item => item.Id == processInstanceId || item.ProcessInstanceId == processInstanceId, cancellationToken);
        return execution?.Id ?? processInstanceId;
    }

    private async Task<string?> ResolveProcessDefinitionIdAsync(WorkflowBusinessInstanceEntity instance, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(instance.ProcessDefinitionId))
        {
            return instance.ProcessDefinitionId;
        }

        var execution = await databaseAccessor.GetCurrentDb().Queryable<ExecutionEntity>()
            .FirstAsync(item => item.Id == instance.ProcessInstanceId || item.ProcessInstanceId == instance.ProcessInstanceId, cancellationToken);
        return execution?.ProcessDefinitionId;
    }

    private async Task<Dictionary<string, object?>> ResolveRuntimeVariablesAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        var executionId = await ResolveRootExecutionIdAsync(processInstanceId, cancellationToken);
        try
        {
            return await runtimeService.GetVariablesAsync(executionId, null, cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> ResolveActiveActivityIdsAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        var executions = await databaseAccessor.GetCurrentDb().Queryable<ExecutionEntity>()
            .Where(item => (item.Id == processInstanceId || item.ProcessInstanceId == processInstanceId) && item.IsActive && !item.IsEnded)
            .ToListAsync(cancellationToken);
        return executions
            .Select(item => item.ActivityId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<Dictionary<string, object?>> ResolveBusinessVariablesAsync(
        WorkflowBindingEntity binding,
        string businessKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(binding.ModelCode) || string.IsNullOrWhiteSpace(binding.KeyField))
        {
            return [];
        }

        var definition = await runtimeDataModelService.GetPublishedDefinitionAsync(binding.ModelCode, cancellationToken);
        if (!string.Equals(definition.KeyField, binding.KeyField, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("审批绑定主键字段与已发布模型不一致", ErrorCodes.RuntimeDataModelInvalid);
        }

        var detail = await runtimeDataModelService.GetDetailAsync(binding.ModelCode, businessKey, cancellationToken);
        if (!detail.Row.ContainsKey(binding.KeyField) &&
            detail.Fields.All(field => !string.Equals(field.Binding, binding.KeyField, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("运行时业务详情缺少审批绑定主键字段", ErrorCodes.RuntimeDataModelInvalid);
        }

        return detail.Row.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> MergeSubmittedVariables(
        IReadOnlyDictionary<string, object?> businessVariables,
        IReadOnlyDictionary<string, object?>? submittedVariables)
    {
        var result = new Dictionary<string, object?>(businessVariables, StringComparer.OrdinalIgnoreCase);
        foreach (var item in submittedVariables ?? new Dictionary<string, object?>())
        {
            result[item.Key] = item.Value;
        }

        return result;
    }

    private async Task<IReadOnlyList<WorkflowActivityResponse>> GetActivityResponsesAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        var histories = await databaseAccessor.GetCurrentDb().Queryable<HistoricActivityInstanceEntity>()
            .Where(item => item.ProcessInstanceId == processInstanceId)
            .OrderBy(item => item.StartTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return histories
            .Select(item => new WorkflowActivityResponse(
                item.Id,
                item.ActivityId,
                item.ActivityName,
                item.ActivityType,
                item.ExecutionId,
                item.ProcessInstanceId,
                item.StartTime,
                item.EndTime,
                item.StartTime.HasValue && item.EndTime.HasValue ? (long)(item.EndTime.Value - item.StartTime.Value).TotalMilliseconds : null))
            .ToList();
    }

    private async Task<IReadOnlyList<WorkflowTaskListItemResponse>> MapTasksAsync(IReadOnlyList<TaskImplementation> tasks, CancellationToken cancellationToken)
    {
        var linksByTaskId = await GetRuntimeTaskIdentityLinksAsync(tasks.Select(task => task.Id), cancellationToken);
        return tasks
            .Select(task => WorkflowTaskAppService.MapTask(task, linksByTaskId.GetValueOrDefault(task.Id, [])))
            .ToList();
    }

    private async Task<Dictionary<string, List<AsterERP.Workflow.Core.Cmd.IdentityLinkEntity>>> GetRuntimeTaskIdentityLinksAsync(
        IEnumerable<string?> taskIds,
        CancellationToken cancellationToken)
    {
        var normalizedTaskIds = taskIds
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedTaskIds.Length == 0)
        {
            return new Dictionary<string, List<AsterERP.Workflow.Core.Cmd.IdentityLinkEntity>>(StringComparer.OrdinalIgnoreCase);
        }

        var links = await databaseAccessor.GetCurrentDb().Queryable<IdentityLinkEntity>()
            .Where(item => item.TaskId != null && normalizedTaskIds.Contains(item.TaskId))
            .ToListAsync(cancellationToken);

        return links
            .GroupBy(item => item.TaskId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => new AsterERP.Workflow.Core.Cmd.IdentityLinkEntity
                {
                    Id = item.Id,
                    UserId = item.UserId,
                    GroupId = item.GroupId,
                    Type = item.Type,
                    TaskId = item.TaskId,
                    ProcessInstanceId = item.ProcessInstanceId,
                    ProcessDefinitionId = item.ProcessDefinitionId
                }).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<WorkflowIdentityLinkResponse>> GetIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        var runtimeLinks = await databaseAccessor.GetCurrentDb().Queryable<IdentityLinkEntity>()
            .Where(item => item.ProcessInstanceId == processInstanceId)
            .ToListAsync(cancellationToken);
        var historicLinks = await historyService.GetHistoricIdentityLinksForProcessInstanceAsync(processInstanceId, cancellationToken);

        return runtimeLinks
            .Select(MapRuntimeIdentityLink)
            .Concat(historicLinks.Select(MapCoreIdentityLink))
            .GroupBy(item => $"{item.Id}:{item.Type}:{item.UserId}:{item.GroupId}:{item.TaskId}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private async Task<IReadOnlyList<WorkflowTimelineItemResponse>> BuildTimelineAsync(
        WorkflowBusinessInstanceEntity instance,
        IReadOnlyList<WorkflowActivityResponse> activities,
        IReadOnlyList<WorkflowCommentResponse> comments,
        IReadOnlyList<WorkflowAttachmentResponse> attachments,
        IReadOnlyList<WorkflowIdentityLinkResponse> identityLinks,
        CancellationToken cancellationToken)
    {
        var historicTasks = await databaseAccessor.GetCurrentDb().Queryable<HistoricTaskInstanceEntity>()
            .Where(item => item.ProcessInstanceId == instance.ProcessInstanceId)
            .OrderBy(item => item.StartTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var userNames = await identityDisplayService.GetUserDisplayNamesAsync(
            CollectTimelineUserIds(instance, historicTasks, comments, attachments, identityLinks),
            cancellationToken);
        var items = new List<WorkflowTimelineItemResponse>
        {
            new(
                $"{instance.ProcessInstanceId}:start",
                "start",
                "发起流程",
                instance.StartedBy,
                identityDisplayService.ResolveUserName(instance.StartedBy, userNames),
                null,
                null,
                "start",
                instance.Remark,
                instance.StartedAt,
                null,
                null,
                new Dictionary<string, object?>
                {
                    ["businessType"] = instance.BusinessType,
                    ["businessKey"] = instance.BusinessKey,
                    ["status"] = instance.Status
                })
        };

        items.AddRange(activities.Select(activity => new WorkflowTimelineItemResponse(
            activity.Id,
            "activity",
            string.IsNullOrWhiteSpace(activity.ActivityName) ? activity.ActivityId ?? "流程节点" : activity.ActivityName!,
            null,
            null,
            activity.ActivityId,
            null,
            activity.ActivityType,
            null,
            activity.StartTime,
            activity.EndTime,
            activity.DurationInMillis,
            new Dictionary<string, object?>
            {
                ["executionId"] = activity.ExecutionId,
                ["processInstanceId"] = activity.ProcessInstanceId
            })));

        items.AddRange(historicTasks.Select(task => new WorkflowTimelineItemResponse(
            task.Id,
            "task",
            string.IsNullOrWhiteSpace(task.Name) ? task.TaskDefinitionKey ?? "审批任务" : task.Name!,
            task.Assignee ?? task.Owner,
            identityDisplayService.ResolveUserName(task.Assignee ?? task.Owner, userNames),
            task.TaskDefinitionKey,
            task.Id,
            task.EndTime is null ? "task-running" : task.DeleteReason ?? "task-finished",
            task.DeleteReason,
            task.StartTime,
            task.EndTime,
            task.StartTime.HasValue && task.EndTime.HasValue ? (long)(task.EndTime.Value - task.StartTime.Value).TotalMilliseconds : null,
            new Dictionary<string, object?>
            {
                ["assignee"] = task.Assignee,
                ["owner"] = task.Owner,
                ["processDefinitionId"] = task.ProcessDefinitionId
            })));

        items.AddRange(comments.Select(comment => new WorkflowTimelineItemResponse(
            comment.Id,
            "comment",
            string.IsNullOrWhiteSpace(comment.Type) ? "审批意见" : $"审批意见:{comment.Type}",
            comment.UserId,
            identityDisplayService.ResolveUserName(comment.UserId, userNames),
            null,
            comment.TaskId,
            comment.Type,
            comment.Message,
            comment.Time,
            null,
            null,
            new Dictionary<string, object?>
            {
                ["processInstanceId"] = comment.ProcessInstanceId
            })));

        items.AddRange(attachments.Select(attachment => new WorkflowTimelineItemResponse(
            attachment.Id,
            "attachment",
            string.IsNullOrWhiteSpace(attachment.Name) ? "审批附件" : attachment.Name!,
            null,
            null,
            null,
            attachment.TaskId,
            attachment.Type,
            attachment.Description,
            null,
            null,
            null,
            new Dictionary<string, object?>
            {
                ["url"] = attachment.Url,
                ["processInstanceId"] = attachment.ProcessInstanceId
            })));

        items.AddRange(identityLinks.Select(link => new WorkflowTimelineItemResponse(
            link.Id,
            "identity",
            ResolveIdentityLinkTitle(link, userNames, identityDisplayService),
            link.UserId,
            identityDisplayService.ResolveUserName(link.UserId, userNames),
            null,
            link.TaskId,
            link.Type,
            link.GroupId,
            instance.StartedAt,
            null,
            null,
            new Dictionary<string, object?>
            {
                ["processDefinitionId"] = link.ProcessDefinitionId,
                ["processInstanceId"] = link.ProcessInstanceId
            })));

        return items
            .OrderBy(item => item.CreatedAt ?? DateTime.MaxValue)
            .ThenBy(item => item.Kind)
            .ToList();
    }

    private static IEnumerable<string?> CollectTimelineUserIds(
        WorkflowBusinessInstanceEntity instance,
        IReadOnlyList<HistoricTaskInstanceEntity> tasks,
        IReadOnlyList<WorkflowCommentResponse> comments,
        IReadOnlyList<WorkflowAttachmentResponse> attachments,
        IReadOnlyList<WorkflowIdentityLinkResponse> identityLinks)
    {
        yield return instance.StartedBy;
        foreach (var task in tasks)
        {
            yield return task.Assignee;
            yield return task.Owner;
        }

        foreach (var comment in comments)
        {
            yield return comment.UserId;
        }

        foreach (var attachment in attachments)
        {
            yield return attachment.Id;
        }

        foreach (var link in identityLinks)
        {
            yield return link.UserId;
        }
    }

    private static string ResolveIdentityLinkTitle(
        WorkflowIdentityLinkResponse link,
        IReadOnlyDictionary<string, string> userNames,
        IWorkflowIdentityDisplayService identityDisplayService)
    {
        var target = !string.IsNullOrWhiteSpace(link.UserId)
            ? identityDisplayService.ResolveUserName(link.UserId, userNames)
            : link.GroupId;
        return $"{link.Type ?? "identity"}:{target ?? "unknown"}";
    }

    private static WorkflowIdentityLinkResponse MapRuntimeIdentityLink(IdentityLinkEntity link)
    {
        return new WorkflowIdentityLinkResponse(
            link.Id,
            link.UserId,
            link.GroupId,
            link.Type,
            link.TaskId,
            link.ProcessInstanceId,
            link.ProcessDefinitionId);
    }

    private static WorkflowIdentityLinkResponse MapCoreIdentityLink(CoreIdentityLinkEntity link)
    {
        return new WorkflowIdentityLinkResponse(
            link.Id,
            link.UserId,
            link.GroupId,
            link.Type,
            link.TaskId,
            link.ProcessInstanceId,
            link.ProcessDefinitionId);
    }

    private static WorkflowCommentResponse MapComment(AsterERP.Workflow.Core.Cmd.CommentEntity comment)
    {
        return new WorkflowCommentResponse(
            comment.Id,
            comment.TaskId,
            comment.ProcessInstanceId,
            comment.Type,
            comment.UserId,
            comment.FullMessage ?? comment.Message,
            comment.Time);
    }

    private static WorkflowAttachmentResponse MapAttachment(AsterERP.Workflow.Core.Cmd.AttachmentEntity attachment)
    {
        return new WorkflowAttachmentResponse(
            attachment.Id,
            attachment.TaskId,
            attachment.ProcessInstanceId,
            attachment.Name,
            attachment.Description,
            attachment.Type,
            attachment.Url)
        {
            HasContent = !string.IsNullOrWhiteSpace(attachment.ContentId),
            DownloadUrl = ResolveAttachmentDownloadUrl(attachment.Id, attachment.ContentId),
            CreatedAt = attachment.Time
        };
    }

    private static string? ResolveAttachmentDownloadUrl(string attachmentId, string? contentId)
    {
        return string.IsNullOrWhiteSpace(contentId)
            ? null
            : $"/api/workflows/tasks/attachments/{Uri.EscapeDataString(attachmentId)}/download";
    }

    private static WorkflowInstanceListItemResponse MapListItem(WorkflowBusinessInstanceEntity item)
    {
        return new WorkflowInstanceListItemResponse(
            item.Id,
            item.TenantId,
            item.AppCode,
            item.MenuCode,
            item.BusinessType,
            item.BusinessKey,
            item.ProcessInstanceId,
            item.ProcessDefinitionId,
            item.ProcessDefinitionKey,
            item.Status,
            item.StartedBy,
            item.StartedAt,
            item.FinishedAt);
    }

    private static string Normalize(string value, string? fallback, string message)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value;
        normalized = normalized?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }
}

