using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Application.Workflows.Callbacks;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Services;
using SqlSugar;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using PersistenceAttachmentEntity = AsterERP.Workflow.Persistence.Entities.AttachmentEntity;
using PersistenceCommentEntity = AsterERP.Workflow.Persistence.Entities.CommentEntity;
using PersistenceHistoricIdentityLinkEntity = AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity;
using PersistenceHistoricTaskInstanceEntity = AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity;
using PersistenceIdentityLinkEntity = AsterERP.Workflow.Persistence.Entities.IdentityLinkEntity;
using PersistenceProcessDefinitionEntity = AsterERP.Workflow.Persistence.Entities.ProcessDefinitionEntity;
using PersistenceTaskEntity = AsterERP.Workflow.Persistence.Entities.TaskEntity;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowTaskAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IWorkflowCurrentUserContext currentUserContext,
    IWorkflowIdentityDisplayService identityDisplayService,
    WorkflowTaskVisibilityService taskVisibilityService,
    WorkflowTaskNodePolicyResolver taskNodePolicyResolver,
    ITaskService taskService,
    WorkflowInstanceAppService instanceService,
    IWorkflowFormResourceAppService formResourceService,
    WorkflowCallbackExecutor callbackExecutor,
    IClock clock,
    IUnitOfWorkManager unitOfWorkManager) : IWorkflowTaskAppService
{
    public async Task<WorkflowTaskSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = currentUserContext.UserId;
        var candidateTaskIds = await taskVisibilityService.GetCandidateTaskIdsAsync(cancellationToken);
        var delegatedTaskIds = await taskVisibilityService.GetDelegatedTaskIdsAsync(cancellationToken);
        var doneCount = await databaseAccessor.GetCurrentDb().Queryable<PersistenceHistoricTaskInstanceEntity>()
            .Where(item => item.EndTime != null && (item.Assignee == currentUserId || item.Owner == currentUserId))
            .CountAsync(cancellationToken);
        var mineCount = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
            .Where(item => !item.IsDeleted && item.StartedBy == currentUserId)
            .CountAsync(cancellationToken);
        var ccCount = await taskVisibilityService.CountCcInstancesAsync(cancellationToken);
        var todoCount = await taskVisibilityService.BuildVisibleRuntimeTaskQuery(candidateTaskIds, delegatedTaskIds).CountAsync(cancellationToken);

        return new WorkflowTaskSummaryResponse(
            todoCount,
            doneCount,
            mineCount,
            await databaseAccessor.GetCurrentDb().Queryable<PersistenceTaskEntity>()
                .Where(item => item.DelegationState != null && (item.Assignee == currentUserId || item.Owner == currentUserId))
                .CountAsync(cancellationToken),
            await taskVisibilityService.BuildVisibleRuntimeTaskQuery(candidateTaskIds, delegatedTaskIds)
                .Where(item => item.DueDate != null && item.DueDate < clock.Now)
                .CountAsync(cancellationToken),
            ccCount,
            await databaseAccessor.GetCurrentDb().Queryable<PersistenceHistoricTaskInstanceEntity>().CountAsync(cancellationToken));
    }

    public async Task<GridPageResult<WorkflowTaskListItemResponse>> GetTodoAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var candidateTaskIds = await taskVisibilityService.GetCandidateTaskIdsAsync(cancellationToken);
        var delegatedTaskIds = await taskVisibilityService.GetDelegatedTaskIdsAsync(cancellationToken);
        var taskQuery = ApplyRuntimeTaskKeyword(taskVisibilityService.BuildVisibleRuntimeTaskQuery(candidateTaskIds, delegatedTaskIds), query)
            .OrderBy(item => item.CreateTime, OrderByType.Desc);
        return await PageRuntimeTaskQueryAsync(taskQuery, query, cancellationToken);
    }

    public async Task<GridPageResult<WorkflowHistoricTaskResponse>> GetDoneAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var currentUserId = currentUserContext.UserId;
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var entities = await databaseAccessor.GetCurrentDb().Queryable<PersistenceHistoricTaskInstanceEntity>()
            .Where(item => item.EndTime != null && (item.Assignee == currentUserId || item.Owner == currentUserId))
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.Name!.Contains(query.Keyword!) || item.ProcessInstanceId!.Contains(query.Keyword!))
            .OrderBy(item => item.EndTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);
        var page = entities.Select(MapHistoricTask).ToList();

        return new GridPageResult<WorkflowHistoricTaskResponse>
        {
            Total = total.Value,
            Items = await EnrichHistoricTasksAsync(page, cancellationToken)
        };
    }

    public Task<GridPageResult<WorkflowInstanceListItemResponse>> GetMineAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        return instanceService.GetMineAsync(query, cancellationToken);
    }

    public async Task<GridPageResult<WorkflowTaskListItemResponse>> GetDelegatedAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var currentUserId = currentUserContext.UserId;
        var taskQuery = ApplyRuntimeTaskKeyword(
                databaseAccessor.GetCurrentDb().Queryable<PersistenceTaskEntity>()
                    .Where(item => item.DelegationState != null && (item.Assignee == currentUserId || item.Owner == currentUserId)),
                query)
            .OrderBy(item => item.CreateTime, OrderByType.Desc);
        return await PageRuntimeTaskQueryAsync(taskQuery, query, cancellationToken);
    }

    public async Task<GridPageResult<WorkflowTaskListItemResponse>> GetTimeoutAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var candidateTaskIds = await taskVisibilityService.GetCandidateTaskIdsAsync(cancellationToken);
        var delegatedTaskIds = await taskVisibilityService.GetDelegatedTaskIdsAsync(cancellationToken);
        var taskQuery = ApplyRuntimeTaskKeyword(
                taskVisibilityService.BuildVisibleRuntimeTaskQuery(candidateTaskIds, delegatedTaskIds)
                    .Where(item => item.DueDate != null && item.DueDate < clock.Now),
                query)
            .OrderBy(item => item.DueDate, OrderByType.Asc);
        return await PageRuntimeTaskQueryAsync(taskQuery, query, cancellationToken);
    }

    public async Task<GridPageResult<WorkflowInstanceListItemResponse>> GetCcAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var items = await ApplyBusinessInstanceFilters(taskVisibilityService.BuildCcBusinessInstanceQuery(), query)
            .OrderBy(item => item.StartedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return new GridPageResult<WorkflowInstanceListItemResponse>
        {
            Total = total.Value,
            Items = items.Select(MapInstanceListItem).ToList()
        };
    }

    public async Task<IReadOnlyList<WorkflowTaskListItemResponse>> GetByProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var tasks = await taskService.GetTasksByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        var linksByTaskId = await GetRuntimeIdentityLinksByTaskIdsAsync(tasks.Select(task => task.Id), cancellationToken);
        var responses = tasks
            .Select(task => MapTask(task, linksByTaskId.GetValueOrDefault(task.Id, [])))
            .ToList();

        return await EnrichRuntimeTasksAsync(responses, cancellationToken);
    }

    public async Task<WorkflowTaskDetailResponse> GetDetailAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        var links = await GetRuntimeIdentityLinksForTaskAsync(task.Id, cancellationToken);
        await EnsureTaskDetailVisibleAsync(task, links, cancellationToken);
        var enrichedTasks = await EnrichRuntimeTasksAsync([MapTask(task, links)], cancellationToken);
        var enrichedTask = enrichedTasks[0];
        var nodePolicy = await taskNodePolicyResolver.ResolveAsync(task.ProcessDefinitionId, task.TaskDefinitionKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(task.ProcessInstanceId))
        {
            return new WorkflowTaskDetailResponse(
                enrichedTask,
                WorkflowSubmittedFormSnapshot.Build(null, null),
                [],
                [],
                [],
                nodePolicy);
        }

        var instance = await GetBusinessInstanceAsync(task.ProcessInstanceId, cancellationToken);
        var instanceDetail = await instanceService.GetDetailAsync(task.ProcessInstanceId, cancellationToken);
        var submittedFormLabels = await formResourceService.GetFieldLabelsForBindingAsync(
            instance.TenantId,
            instance.AppCode,
            instance.MenuCode,
            instance.BusinessType,
            cancellationToken);

        return new WorkflowTaskDetailResponse(
            enrichedTask,
            WorkflowSubmittedFormSnapshot.Build(instance.SubmittedFormJson, instance.VariableSnapshotJson, submittedFormLabels),
            instanceDetail.Comments,
            instanceDetail.Attachments,
            instanceDetail.Timeline,
            nodePolicy);
    }

    public async Task ClaimAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        var links = await GetRuntimeIdentityLinksForTaskAsync(task.Id, cancellationToken);
        await EnsureRuntimeTaskVisibleAsync(task, links, cancellationToken);
        await taskService.ClaimTaskAsync(taskId, currentUserContext.UserId, cancellationToken);
    }

    public async Task UnclaimAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        await EnsureTaskProcessableByCurrentUserAsync(task, cancellationToken);
        await taskService.UnclaimTaskAsync(taskId, cancellationToken);
    }

    public async Task CompleteAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        await CompleteWithActionAsync(taskId, request, "complete", cancellationToken);
    }

    public Task RejectAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        return CompleteWithActionAsync(taskId, request, "reject", cancellationToken);
    }

    public Task ReturnAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        return CompleteWithActionAsync(taskId, request, "return", cancellationToken);
    }

    public async Task TransferAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        await EnsureTaskProcessableByCurrentUserAsync(task, cancellationToken);
        var targetUserId = ResolveTargetUser(request);
        await AddActionCommentAsync(task, request.Comment ?? $"transfer to {targetUserId}", "transfer", cancellationToken);
        await taskService.SetAssigneeAsync(taskId, targetUserId, cancellationToken);
    }

    public async Task DelegateAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        await EnsureTaskProcessableByCurrentUserAsync(task, cancellationToken);
        var targetUserId = ResolveTargetUser(request);
        await AddActionCommentAsync(task, request.Comment ?? $"delegate to {targetUserId}", "delegate", cancellationToken);
        await taskService.DelegateTaskAsync(taskId, targetUserId, cancellationToken);
    }

    public async Task ResolveAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        await EnsureTaskProcessableByCurrentUserAsync(task, cancellationToken);
        await AddActionCommentAsync(task, request.Comment, "resolve", cancellationToken);
        await taskService.ResolveTaskAsync(taskId, request.Variables, cancellationToken);
    }

    public async Task SetOwnerAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        await EnsureTaskProcessableByCurrentUserAsync(task, cancellationToken);
        await taskService.SetOwnerAsync(taskId, ResolveTargetUser(request), cancellationToken);
    }

    public async Task<WorkflowTaskListItemResponse> AddSignAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default)
    {
        var parent = await EnsureTaskExistsAsync(taskId, cancellationToken);
        await EnsureTaskProcessableByCurrentUserAsync(parent, cancellationToken);
        var targetUserId = ResolveTargetUser(request);
        var created = await taskService.CreateTaskAsync(parent with
        {
            Id = null!,
            Name = $"{parent.Name ?? "审批任务"}-加签",
            Assignee = targetUserId,
            Owner = currentUserContext.UserId,
            ParentTaskId = parent.Id,
            CreateTime = clock.Now,
            CandidateUsers = null,
            CandidateGroups = null
        }, cancellationToken);
        await AddActionCommentAsync(parent, request.Comment ?? $"add-sign {targetUserId}", "add-sign", cancellationToken);
        var links = await GetRuntimeIdentityLinksForTaskAsync(created.Id, cancellationToken);
        var enriched = await EnrichRuntimeTasksAsync([MapTask(created, links)], cancellationToken);
        return enriched[0];
    }

    public async Task AddIdentityLinkAsync(string taskId, WorkflowIdentityLinkRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTaskExistsAsync(taskId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            await taskService.AddUserIdentityLinkAsync(taskId, request.UserId.Trim(), NormalizeLinkType(request.Type), cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.GroupId))
        {
            await taskService.AddGroupIdentityLinkAsync(taskId, request.GroupId.Trim(), NormalizeLinkType(request.Type), cancellationToken);
            return;
        }

        throw new ValidationException("用户或组至少传入一个", ErrorCodes.ParameterInvalid);
    }

    public async Task DeleteIdentityLinkAsync(string taskId, WorkflowIdentityLinkRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTaskExistsAsync(taskId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            await taskService.DeleteUserIdentityLinkAsync(taskId, request.UserId.Trim(), NormalizeLinkType(request.Type), cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.GroupId))
        {
            await taskService.DeleteGroupIdentityLinkAsync(taskId, request.GroupId.Trim(), NormalizeLinkType(request.Type), cancellationToken);
            return;
        }

        throw new ValidationException("用户或组至少传入一个", ErrorCodes.ParameterInvalid);
    }

    public async Task<IReadOnlyList<WorkflowIdentityLinkResponse>> GetIdentityLinksAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await EnsureTaskExistsAsync(taskId, cancellationToken);
        return (await GetRuntimeIdentityLinksForTaskAsync(taskId, cancellationToken)).Select(MapIdentityLink).ToList();
    }

    public async Task<WorkflowCommentResponse> AddCommentAsync(string taskId, WorkflowCommentRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        var links = await GetRuntimeIdentityLinksForTaskAsync(task.Id, cancellationToken);
        await EnsureRuntimeTaskVisibleAsync(task, links, cancellationToken);
        var message = NormalizeOptional(request.Message, "评论内容不能为空");
        var comment = await taskService.AddCommentAsync(taskId, task.ProcessInstanceId, request.Type, message, cancellationToken);
        return MapComment(comment);
    }

    public async Task<IReadOnlyList<WorkflowCommentResponse>> GetCommentsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        var links = await GetRuntimeIdentityLinksForTaskAsync(task.Id, cancellationToken);
        await EnsureTaskDetailVisibleAsync(task, links, cancellationToken);
        return (await taskService.GetTaskCommentsAsync(taskId, cancellationToken)).Select(MapComment).ToList();
    }

    public async Task<WorkflowAttachmentResponse> AddAttachmentAsync(string taskId, WorkflowAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        var links = await GetRuntimeIdentityLinksForTaskAsync(task.Id, cancellationToken);
        await EnsureRuntimeTaskVisibleAsync(task, links, cancellationToken);
        var name = NormalizeOptional(request.Name, "附件名称不能为空");
        var attachment = string.IsNullOrWhiteSpace(request.Url)
            ? await taskService.CreateAttachmentAsync(
                request.AttachmentType,
                taskId,
                task.ProcessInstanceId,
                name,
                request.Description,
                string.IsNullOrWhiteSpace(request.Base64Content) ? null : Convert.FromBase64String(request.Base64Content),
                cancellationToken)
            : await taskService.CreateAttachmentAsync(
                request.AttachmentType,
                taskId,
                task.ProcessInstanceId,
                name,
                request.Description,
                request.Url,
                cancellationToken);
        return MapAttachment(attachment);
    }

    public async Task<IReadOnlyList<WorkflowAttachmentResponse>> GetAttachmentsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        var links = await GetRuntimeIdentityLinksForTaskAsync(task.Id, cancellationToken);
        await EnsureTaskDetailVisibleAsync(task, links, cancellationToken);
        return (await taskService.GetTaskAttachmentsAsync(taskId, cancellationToken)).Select(MapAttachment).ToList();
    }

    public async Task<(WorkflowAttachmentResponse Metadata, byte[] Content)> DownloadAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await taskService.GetAttachmentAsync(attachmentId, cancellationToken)
            ?? throw new NotFoundException("审批附件不存在", ErrorCodes.FileNotFound);

        await EnsureAttachmentVisibleAsync(attachment, cancellationToken);

        var content = await taskService.GetAttachmentContentAsync(attachmentId, cancellationToken);
        if (content is null || content.Length == 0)
        {
            throw new NotFoundException("审批附件内容不存在", ErrorCodes.FileNotFound);
        }

        return (MapAttachment(attachment), content);
    }

    internal static WorkflowTaskListItemResponse MapTask(TaskImplementation task, IReadOnlyCollection<IdentityLinkEntity> links)
    {
        var mappedLinks = links.Select(MapIdentityLink).ToList();
        AddCandidateLinks(mappedLinks, task);

        return new WorkflowTaskListItemResponse(
            task.Id,
            task.Name,
            task.Assignee,
            task.Owner,
            task.DelegationState,
            task.ProcessInstanceId,
            task.ProcessDefinitionId,
            null,
            task.TaskDefinitionKey,
            task.Priority,
            task.CreateTime,
            task.DueDate,
            mappedLinks);
    }

    private static WorkflowTaskListItemResponse MapTask(PersistenceTaskEntity task, IReadOnlyCollection<PersistenceIdentityLinkEntity> links)
    {
        return new WorkflowTaskListItemResponse(
            task.Id,
            task.Name,
            task.Assignee,
            task.Owner,
            task.DelegationState,
            task.ProcessInstanceId,
            task.ProcessDefinitionId,
            task.ExecutionId,
            task.TaskDefinitionKey,
            task.Priority,
            task.CreateTime,
            task.DueDate,
            links.Select(MapIdentityLink).ToList());
    }

    private async Task<GridPageResult<WorkflowTaskListItemResponse>> PageRuntimeTaskQueryAsync(
        ISugarQueryable<PersistenceTaskEntity> taskQuery,
        GridQuery query,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var page = await taskQuery.ToPageListAsync(pageIndex, pageSize, total, cancellationToken);
        var taskIds = NormalizeIds(page.Select(item => item.Id));
        List<PersistenceIdentityLinkEntity> links = taskIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<PersistenceIdentityLinkEntity>()
                .Where(item => item.TaskId != null && taskIds.Contains(item.TaskId))
                .ToListAsync(cancellationToken);
        var linksByTask = links
            .GroupBy(item => item.TaskId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var mapped = page
            .Select(task =>
            {
                linksByTask.TryGetValue(task.Id, out var taskLinks);
                return MapTask(task, taskLinks ?? []);
            })
            .ToList();

        return new GridPageResult<WorkflowTaskListItemResponse>
        {
            Total = total.Value,
            Items = await EnrichRuntimeTasksAsync(mapped, cancellationToken)
        };
    }

    private async Task<Dictionary<string, List<IdentityLinkEntity>>> GetRuntimeIdentityLinksByTaskIdsAsync(
        IEnumerable<string?> taskIds,
        CancellationToken cancellationToken)
    {
        var normalizedTaskIds = NormalizeIds(taskIds);
        if (normalizedTaskIds.Count == 0)
        {
            return new Dictionary<string, List<IdentityLinkEntity>>(StringComparer.OrdinalIgnoreCase);
        }

        var links = await databaseAccessor.GetCurrentDb().Queryable<PersistenceIdentityLinkEntity>()
            .Where(item => item.TaskId != null && normalizedTaskIds.Contains(item.TaskId))
            .ToListAsync(cancellationToken);

        return links
            .GroupBy(item => item.TaskId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(MapRuntimeIdentityLinkEntity).ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<IdentityLinkEntity>> GetRuntimeIdentityLinksForTaskAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        var linksByTaskId = await GetRuntimeIdentityLinksByTaskIdsAsync([taskId], cancellationToken);
        return linksByTaskId.TryGetValue(taskId, out var links) ? links : [];
    }

    private static ISugarQueryable<PersistenceTaskEntity> ApplyRuntimeTaskKeyword(
        ISugarQueryable<PersistenceTaskEntity> taskQuery,
        GridQuery query)
    {
        return taskQuery.WhereIF(
            !string.IsNullOrWhiteSpace(query.Keyword),
            item => item.Name!.Contains(query.Keyword!) || item.ProcessInstanceId!.Contains(query.Keyword!));
    }

    private async Task<List<WorkflowTaskListItemResponse>> EnrichRuntimeTasksAsync(
        IReadOnlyList<WorkflowTaskListItemResponse> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var processInstanceIds = NormalizeIds(items.Select(item => item.ProcessInstanceId));
        var taskIds = NormalizeIds(items.Select(item => item.Id));
        var instances = await GetBusinessInstanceMapAsync(processInstanceIds, cancellationToken);
        var processDefinitions = await GetProcessDefinitionNamesAsync(items, instances.Values, cancellationToken);
        var users = await identityDisplayService.GetUserDisplayNamesAsync(CollectRuntimeUserIds(items, instances), cancellationToken);
        var groups = await identityDisplayService.GetGroupDisplayNamesAsync(
            items.SelectMany(item => item.IdentityLinks).Select(link => link.GroupId),
            cancellationToken);
        var comments = await GetCommentCountsAsync(taskIds, processInstanceIds, cancellationToken);
        var attachments = await GetAttachmentCountsAsync(taskIds, processInstanceIds, cancellationToken);
        var delegatedTaskIds = await taskVisibilityService.GetDelegatedTaskIdsAsync(taskIds, cancellationToken);

        return items.Select(item =>
        {
            instances.TryGetValue(item.ProcessInstanceId ?? string.Empty, out var instance);
            var assigneeName = identityDisplayService.ResolveUserName(item.Assignee, users);
            var candidateNames = item.IdentityLinks
                .Where(link => string.Equals(link.Type, IdentityLinkType.CANDIDATE, StringComparison.OrdinalIgnoreCase))
                .Select(link => identityDisplayService.ResolveCandidateName(link, users, groups))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var enriched = item with
            {
                BusinessType = instance?.BusinessType,
                BusinessKey = instance?.BusinessKey,
                ProcessName = ResolveProcessName(item, instance, processDefinitions),
                StarterUserName = identityDisplayService.ResolveUserName(instance?.StartedBy, users),
                AssigneeName = assigneeName,
                CandidateNames = candidateNames,
                IsClaimable = item.Assignee is null && taskVisibilityService.IsTaskVisibleToCurrentUser(item),
                IsOverdue = item.DueAt.HasValue && item.DueAt.Value < clock.Now,
                CommentsCount = CountForTaskAndProcess(comments, item.Id, item.ProcessInstanceId),
                AttachmentsCount = CountForTaskAndProcess(attachments, item.Id, item.ProcessInstanceId)
            };

            return enriched with
            {
                AvailableActions = ResolveAvailableActions(enriched, delegatedTaskIds.Contains(enriched.Id, StringComparer.OrdinalIgnoreCase))
            };
        }).ToList();
    }

    private async Task<List<WorkflowHistoricTaskResponse>> EnrichHistoricTasksAsync(
        IReadOnlyList<WorkflowHistoricTaskResponse> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var processInstanceIds = NormalizeIds(items.Select(item => item.ProcessInstanceId));
        var taskIds = NormalizeIds(items.Select(item => item.Id));
        var instances = await GetBusinessInstanceMapAsync(processInstanceIds, cancellationToken);
        var processDefinitions = await GetProcessDefinitionNamesAsync(items, instances.Values, cancellationToken);
        var users = await identityDisplayService.GetUserDisplayNamesAsync(CollectHistoricUserIds(items, instances), cancellationToken);
        var comments = await GetCommentCountsAsync(taskIds, processInstanceIds, cancellationToken);
        var attachments = await GetAttachmentCountsAsync(taskIds, processInstanceIds, cancellationToken);

        return items.Select(item =>
        {
            instances.TryGetValue(item.ProcessInstanceId ?? string.Empty, out var instance);
            return item with
            {
                BusinessType = instance?.BusinessType,
                BusinessKey = instance?.BusinessKey,
                ProcessName = ResolveProcessName(item.ProcessDefinitionId, instance, processDefinitions),
                StarterUserName = identityDisplayService.ResolveUserName(instance?.StartedBy, users),
                AssigneeName = identityDisplayService.ResolveUserName(item.Assignee, users),
                CommentsCount = CountForTaskAndProcess(comments, item.Id, item.ProcessInstanceId),
                AttachmentsCount = CountForTaskAndProcess(attachments, item.Id, item.ProcessInstanceId)
            };
        }).ToList();
    }

    private async Task<Dictionary<string, WorkflowBusinessInstanceEntity>> GetBusinessInstanceMapAsync(
        IReadOnlyCollection<string> processInstanceIds,
        CancellationToken cancellationToken)
    {
        if (processInstanceIds.Count == 0)
        {
            return new Dictionary<string, WorkflowBusinessInstanceEntity>(StringComparer.OrdinalIgnoreCase);
        }

        var instances = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
            .Where(item => !item.IsDeleted && processInstanceIds.Contains(item.ProcessInstanceId))
            .ToListAsync(cancellationToken);
        return instances.ToDictionary(item => item.ProcessInstanceId, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, string>> GetProcessDefinitionNamesAsync(
        IEnumerable<WorkflowTaskListItemResponse> tasks,
        IEnumerable<WorkflowBusinessInstanceEntity> instances,
        CancellationToken cancellationToken)
    {
        var definitionIds = NormalizeIds(tasks.Select(item => item.ProcessDefinitionId)
            .Concat(instances.Select(item => item.ProcessDefinitionId)));
        return await GetProcessDefinitionNamesAsync(definitionIds, cancellationToken);
    }

    private async Task<Dictionary<string, string>> GetProcessDefinitionNamesAsync(
        IEnumerable<WorkflowHistoricTaskResponse> tasks,
        IEnumerable<WorkflowBusinessInstanceEntity> instances,
        CancellationToken cancellationToken)
    {
        var definitionIds = NormalizeIds(tasks.Select(item => item.ProcessDefinitionId)
            .Concat(instances.Select(item => item.ProcessDefinitionId)));
        return await GetProcessDefinitionNamesAsync(definitionIds, cancellationToken);
    }

    private async Task<Dictionary<string, string>> GetProcessDefinitionNamesAsync(
        IReadOnlyCollection<string> processDefinitionIds,
        CancellationToken cancellationToken)
    {
        if (processDefinitionIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var definitions = await databaseAccessor.GetCurrentDb().Queryable<PersistenceProcessDefinitionEntity>()
            .Where(item => processDefinitionIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        return definitions.ToDictionary(
            item => item.Id,
            item => string.IsNullOrWhiteSpace(item.Name) ? item.Key ?? item.Id : item.Name!,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, int>> GetCommentCountsAsync(
        IReadOnlyCollection<string> taskIds,
        IReadOnlyCollection<string> processInstanceIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0 && processInstanceIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var comments = await databaseAccessor.GetCurrentDb().Queryable<PersistenceCommentEntity>()
            .Where(item =>
                (item.TaskId != null && taskIds.Contains(item.TaskId)) ||
                (item.ProcessInstanceId != null && processInstanceIds.Contains(item.ProcessInstanceId)))
            .ToListAsync(cancellationToken);
        return BuildTaskProcessCountMap(comments.Select(item => (item.TaskId, item.ProcessInstanceId)));
    }

    private async Task<Dictionary<string, int>> GetAttachmentCountsAsync(
        IReadOnlyCollection<string> taskIds,
        IReadOnlyCollection<string> processInstanceIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0 && processInstanceIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var attachments = await databaseAccessor.GetCurrentDb().Queryable<PersistenceAttachmentEntity>()
            .Where(item =>
                (item.TaskId != null && taskIds.Contains(item.TaskId)) ||
                (item.ProcessInstanceId != null && processInstanceIds.Contains(item.ProcessInstanceId)))
            .ToListAsync(cancellationToken);
        return BuildTaskProcessCountMap(attachments.Select(item => (item.TaskId, item.ProcessInstanceId)));
    }

    private static ISugarQueryable<WorkflowBusinessInstanceEntity> ApplyBusinessInstanceFilters(
        ISugarQueryable<WorkflowBusinessInstanceEntity> query,
        GridQuery filters)
    {
        return query
            .WhereIF(!string.IsNullOrWhiteSpace(filters.TenantId), item => item.TenantId == filters.TenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(filters.AppCode), item => item.AppCode == filters.AppCode)
            .WhereIF(!string.IsNullOrWhiteSpace(filters.Status), item => item.Status == filters.Status)
            .WhereIF(
                !string.IsNullOrWhiteSpace(filters.Keyword),
                item => item.BusinessKey.Contains(filters.Keyword!) ||
                        item.BusinessType.Contains(filters.Keyword!) ||
                        item.ProcessDefinitionKey.Contains(filters.Keyword!));
    }

    private async Task CompleteWithActionAsync(string taskId, WorkflowTaskActionRequest request, string action, CancellationToken cancellationToken)
    {
        var task = await EnsureTaskExistsAsync(taskId, cancellationToken);
        var isDelegatedApproval = await EnsureTaskProcessableByCurrentUserAsync(task, cancellationToken);
        if (isDelegatedApproval)
        {
            task = await AssignDelegatedTaskToCurrentUserAsync(task, cancellationToken);
        }

        await ValidateTaskActionPolicyAsync(task, request, action, cancellationToken);
        var variables = new Dictionary<string, object?>(request.Variables ?? [], StringComparer.OrdinalIgnoreCase)
        {
            ["approvalAction"] = action,
            ["approvalUserId"] = currentUserContext.UserId,
            ["previousApproverUserId"] = currentUserContext.UserId,
            ["approvalComment"] = request.Comment
        };

        await AddActionCommentAsync(task, request.Comment, action, cancellationToken);
        await taskService.CompleteTaskAsync(taskId, variables, cancellationToken);
        if (!string.IsNullOrWhiteSpace(task.ProcessInstanceId))
        {
            await ExecuteInWorkflowTransactionAsync(async () =>
            {
                var instance = await GetBusinessInstanceAsync(task.ProcessInstanceId, cancellationToken);
                await UpdateVariableSnapshotAsync(instance, variables, cancellationToken);
                await callbackExecutor.ExecuteAsync(
                    BuildCallbackContext(
                        instance,
                        ResolveTaskCallbackTrigger(action),
                        task.TaskDefinitionKey,
                        task.Id,
                        action,
                        variables),
                    cancellationToken);
                await instanceService.QueueInstanceNotificationAsync(instance, $"task-{action}", task.TaskDefinitionKey, cancellationToken);
                var completed = await instanceService.MarkCompletedIfEndedAsync(task.ProcessInstanceId, cancellationToken);
                if (!completed)
                {
                    await ExecuteNodeEnterCallbacksAsync(instance, action, variables, cancellationToken);
                await instanceService.QueueRuntimeTaskNotificationsAsync(instance, "node-enter", cancellationToken);
                }
            }, cancellationToken);
        }
    }

    private async Task ValidateTaskActionPolicyAsync(
        TaskImplementation task,
        WorkflowTaskActionRequest request,
        string action,
        CancellationToken cancellationToken)
    {
        var nodePolicy = await taskNodePolicyResolver.ResolveAsync(task.ProcessDefinitionId, task.TaskDefinitionKey, cancellationToken);
        if (nodePolicy.ActionPolicies.Count == 0)
        {
            return;
        }

        var policy = nodePolicy.ActionPolicies.FirstOrDefault(item => string.Equals(item.Action, action, StringComparison.OrdinalIgnoreCase));
        if (policy is null)
        {
            return;
        }

        if (!policy.Enabled)
        {
            throw new ValidationException("当前节点不允许执行该审批动作", ErrorCodes.WorkflowActionInvalid);
        }

        if (policy.CommentRequired && string.IsNullOrWhiteSpace(request.Comment))
        {
            throw new ValidationException("当前节点要求填写审批意见", ErrorCodes.WorkflowActionInvalid);
        }

        var attachmentPolicy = string.IsNullOrWhiteSpace(policy.AttachmentPolicy)
            ? "optional"
            : policy.AttachmentPolicy.Trim();
        if (string.Equals(attachmentPolicy, "optional", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var attachmentCount = (await taskService.GetTaskAttachmentsAsync(task.Id, cancellationToken)).Count;
        if (string.Equals(attachmentPolicy, "required", StringComparison.OrdinalIgnoreCase) && attachmentCount == 0)
        {
            throw new ValidationException("当前节点要求至少上传一个审批附件", ErrorCodes.WorkflowActionInvalid);
        }

        if (string.Equals(attachmentPolicy, "none", StringComparison.OrdinalIgnoreCase) && attachmentCount > 0)
        {
            throw new ValidationException("当前节点不允许上传审批附件", ErrorCodes.WorkflowActionInvalid);
        }
    }

    private async Task UpdateVariableSnapshotAsync(
        WorkflowBusinessInstanceEntity instance,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        var mergedVariables = WorkflowJson.DeserializeVariables(instance.VariableSnapshotJson);
        foreach (var variable in variables)
        {
            mergedVariables[variable.Key] = variable.Value;
        }

        instance.VariableSnapshotJson = WorkflowJson.Serialize(mergedVariables);
        instance.UpdatedBy = currentUserContext.UserId;
        instance.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(instance).ExecuteCommandAsync(cancellationToken);
    }

    private async Task ExecuteNodeEnterCallbacksAsync(
        WorkflowBusinessInstanceEntity instance,
        string action,
        IReadOnlyDictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        var tasks = await taskService.GetTasksByProcessInstanceIdAsync(instance.ProcessInstanceId, cancellationToken);
        foreach (var task in tasks)
        {
            await callbackExecutor.ExecuteAsync(
                BuildCallbackContext(
                    instance,
                    WorkflowCallbackTriggers.NodeEnter,
                    task.TaskDefinitionKey,
                    task.Id,
                    action,
                    variables),
                cancellationToken);
        }
    }

    private WorkflowCallbackContext BuildCallbackContext(
        WorkflowBusinessInstanceEntity instance,
        string trigger,
        string? nodeId,
        string? workflowTaskId,
        string? action,
        IReadOnlyDictionary<string, object?> variables)
    {
        var mergedVariables = WorkflowJson.DeserializeVariables(instance.VariableSnapshotJson);
        foreach (var variable in variables)
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

    private static string ResolveTaskCallbackTrigger(string action)
    {
        return action switch
        {
            "reject" => WorkflowCallbackTriggers.TaskReject,
            "return" => WorkflowCallbackTriggers.TaskReturn,
            _ => WorkflowCallbackTriggers.TaskComplete
        };
    }

    private async Task ExecuteInWorkflowTransactionAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        using var unitOfWork = unitOfWorkManager.Begin();
        await action();
        await unitOfWork.CompleteAsync(cancellationToken);
    }

    private async Task<TaskImplementation> EnsureTaskExistsAsync(string taskId, CancellationToken cancellationToken)
    {
        return await taskService.GetTaskAsync(taskId, cancellationToken)
            ?? throw new NotFoundException("审批任务不存在", ErrorCodes.WorkflowTaskNotFound);
    }

    private async Task<WorkflowBusinessInstanceEntity> GetBusinessInstanceAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
            .FirstAsync(item => item.ProcessInstanceId == processInstanceId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("流程实例不存在", ErrorCodes.WorkflowInstanceNotFound);
    }

    private async Task EnsureRuntimeTaskVisibleAsync(TaskImplementation task, IReadOnlyCollection<IdentityLinkEntity> links, CancellationToken cancellationToken)
    {
        if (!taskVisibilityService.IsRuntimeTaskVisibleToCurrentUser(task, links) &&
            !await taskVisibilityService.IsRuntimeTaskDelegatedToCurrentUserAsync(task, cancellationToken))
        {
            throw new ValidationException("无权限访问该审批任务", ErrorCodes.PermissionDenied);
        }
    }

    private async Task EnsureTaskDetailVisibleAsync(TaskImplementation task, IReadOnlyCollection<IdentityLinkEntity> links, CancellationToken cancellationToken)
    {
        if (taskVisibilityService.IsRuntimeTaskVisibleToCurrentUser(task, links))
        {
            return;
        }

        if (await taskVisibilityService.IsRuntimeTaskDelegatedToCurrentUserAsync(task, cancellationToken))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(task.ProcessInstanceId) &&
            await taskVisibilityService.IsProcessInstanceVisibleToCurrentUserAsync(task.ProcessInstanceId, cancellationToken))
        {
            return;
        }

        throw new ValidationException("无权限访问该审批任务", ErrorCodes.PermissionDenied);
    }

    private void EnsureTaskAssignedToCurrentUser(TaskImplementation task)
    {
        if (!string.Equals(task.Assignee, currentUserContext.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("无权限处理该审批任务", ErrorCodes.PermissionDenied);
        }
    }

    private async Task<bool> EnsureTaskProcessableByCurrentUserAsync(TaskImplementation task, CancellationToken cancellationToken)
    {
        if (string.Equals(task.Assignee, currentUserContext.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (await taskVisibilityService.IsRuntimeTaskDelegatedToCurrentUserAsync(task, cancellationToken))
        {
            return true;
        }

        throw new ValidationException("无权限处理该审批任务", ErrorCodes.PermissionDenied);
    }

    private async Task<TaskImplementation> AssignDelegatedTaskToCurrentUserAsync(TaskImplementation task, CancellationToken cancellationToken)
    {
        var originalAssignee = task.Assignee ?? task.Owner;
        if (!string.IsNullOrWhiteSpace(originalAssignee))
        {
            await taskService.SetOwnerAsync(task.Id, originalAssignee, cancellationToken);
        }

        await taskService.SetAssigneeAsync(task.Id, currentUserContext.UserId, cancellationToken);
        return task with
        {
            Owner = originalAssignee,
            Assignee = currentUserContext.UserId
        };
    }

    private async Task EnsureAttachmentVisibleAsync(AttachmentEntity attachment, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(attachment.TaskId))
        {
            var task = await taskService.GetTaskAsync(attachment.TaskId, cancellationToken);
            if (task is not null)
            {
                var links = await GetRuntimeIdentityLinksForTaskAsync(task.Id, cancellationToken);
                if (taskVisibilityService.IsRuntimeTaskVisibleToCurrentUser(task, links))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(task.ProcessInstanceId) &&
                    await taskVisibilityService.IsProcessInstanceVisibleToCurrentUserAsync(task.ProcessInstanceId, cancellationToken))
                {
                    return;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(attachment.ProcessInstanceId) &&
            await taskVisibilityService.IsProcessInstanceVisibleToCurrentUserAsync(attachment.ProcessInstanceId, cancellationToken))
        {
            return;
        }

        throw new ValidationException("无权限访问该审批附件", ErrorCodes.PermissionDenied);
    }

    private async Task AddActionCommentAsync(TaskImplementation task, string? comment, string action, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return;
        }

        await taskService.AddCommentAsync(task.Id, task.ProcessInstanceId, action, comment.Trim(), cancellationToken);
    }

    private IReadOnlyList<string> ResolveAvailableActions(WorkflowTaskListItemResponse task, bool isDelegatedToCurrentUser)
    {
        var actions = new List<string>();
        var isAssignee = string.Equals(task.Assignee, currentUserContext.UserId, StringComparison.OrdinalIgnoreCase);
        var isOwner = string.Equals(task.Owner, currentUserContext.UserId, StringComparison.OrdinalIgnoreCase);

        if (task.IsClaimable)
        {
            actions.Add("claim");
        }

        if (isAssignee || isDelegatedToCurrentUser)
        {
            if (!string.IsNullOrWhiteSpace(task.DelegationState))
            {
                actions.Add("resolve");
            }

            actions.AddRange(["complete", "reject", "return", "transfer", "delegate", "add-sign"]);
        }

        if (isAssignee || isOwner || task.IsClaimable || isDelegatedToCurrentUser)
        {
            actions.AddRange(["comment", "attachment"]);
        }

        return actions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyCollection<string> NormalizeIds(IEnumerable<string?> ids)
    {
        return ids
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string?> CollectRuntimeUserIds(
        IReadOnlyList<WorkflowTaskListItemResponse> tasks,
        IReadOnlyDictionary<string, WorkflowBusinessInstanceEntity> instances)
    {
        foreach (var task in tasks)
        {
            yield return task.Assignee;
            yield return task.Owner;
            if (!string.IsNullOrWhiteSpace(task.ProcessInstanceId) &&
                instances.TryGetValue(task.ProcessInstanceId, out var instance))
            {
                yield return instance.StartedBy;
            }

            foreach (var link in task.IdentityLinks)
            {
                yield return link.UserId;
            }
        }
    }

    private static IEnumerable<string?> CollectHistoricUserIds(
        IReadOnlyList<WorkflowHistoricTaskResponse> tasks,
        IReadOnlyDictionary<string, WorkflowBusinessInstanceEntity> instances)
    {
        foreach (var task in tasks)
        {
            yield return task.Assignee;
            yield return task.Owner;
            if (!string.IsNullOrWhiteSpace(task.ProcessInstanceId) &&
                instances.TryGetValue(task.ProcessInstanceId, out var instance))
            {
                yield return instance.StartedBy;
            }
        }
    }

    private static string? ResolveProcessName(
        WorkflowTaskListItemResponse task,
        WorkflowBusinessInstanceEntity? instance,
        IReadOnlyDictionary<string, string> processDefinitions)
    {
        return ResolveProcessName(task.ProcessDefinitionId, instance, processDefinitions);
    }

    private static string? ResolveProcessName(
        string? processDefinitionId,
        WorkflowBusinessInstanceEntity? instance,
        IReadOnlyDictionary<string, string> processDefinitions)
    {
        var definitionId = processDefinitionId ?? instance?.ProcessDefinitionId;
        if (!string.IsNullOrWhiteSpace(definitionId) && processDefinitions.TryGetValue(definitionId, out var name))
        {
            return name;
        }

        return instance?.ProcessDefinitionKey ?? processDefinitionId;
    }

    private static Dictionary<string, int> BuildTaskProcessCountMap(IEnumerable<(string? TaskId, string? ProcessInstanceId)> items)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (taskId, processInstanceId) in items)
        {
            IncrementCount(result, TaskCountKey("task", taskId));
            IncrementCount(result, TaskCountKey("process", processInstanceId));
        }

        return result;
    }

    private static int CountForTaskAndProcess(
        IReadOnlyDictionary<string, int> counts,
        string taskId,
        string? processInstanceId)
    {
        return GetCount(counts, TaskCountKey("task", taskId)) +
               GetCount(counts, TaskCountKey("process", processInstanceId));
    }

    private static int GetCount(IReadOnlyDictionary<string, int> counts, string? key)
    {
        return key is not null && counts.TryGetValue(key, out var count) ? count : 0;
    }

    private static void IncrementCount(IDictionary<string, int> counts, string? key)
    {
        if (key is null)
        {
            return;
        }

        counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private static string? TaskCountKey(string scope, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"{scope}:{value}";
    }

    private static WorkflowInstanceListItemResponse MapInstanceListItem(WorkflowBusinessInstanceEntity item)
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

    private static string ResolveTargetUser(WorkflowTaskActionRequest request)
    {
        return NormalizeOptional(request.TargetUserId, "目标用户不能为空");
    }

    private static string NormalizeOptional(string? value, string message)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string NormalizeLinkType(string type)
    {
        return NormalizeOptional(type, "身份链路类型不能为空");
    }

    private static WorkflowHistoricTaskResponse MapHistoricTask(HistoricTaskInstance task)
    {
        return new WorkflowHistoricTaskResponse(
            task.Id,
            task.Name,
            task.Assignee,
            task.Owner,
            task.ProcessInstanceId,
            task.TaskDefinitionKey,
            task.StartTime,
            task.EndTime,
            task.StartTime.HasValue && task.EndTime.HasValue ? (long)(task.EndTime.Value - task.StartTime.Value).TotalMilliseconds : null,
            task.DeleteReason)
        {
            ProcessDefinitionId = task.ProcessDefinitionId
        };
    }

    private static WorkflowHistoricTaskResponse MapHistoricTask(PersistenceHistoricTaskInstanceEntity task)
    {
        return new WorkflowHistoricTaskResponse(
            task.Id,
            task.Name,
            task.Assignee,
            task.Owner,
            task.ProcessInstanceId,
            task.TaskDefinitionKey,
            task.StartTime,
            task.EndTime,
            task.DurationInMillis,
            task.DeleteReason)
        {
            ProcessDefinitionId = task.ProcessDefinitionId
        };
    }

    private static WorkflowIdentityLinkResponse MapIdentityLink(IdentityLinkEntity link)
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

    private static WorkflowIdentityLinkResponse MapIdentityLink(PersistenceIdentityLinkEntity link)
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

    private static IdentityLinkEntity MapRuntimeIdentityLinkEntity(PersistenceIdentityLinkEntity link)
    {
        return new IdentityLinkEntity
        {
            Id = link.Id,
            UserId = link.UserId,
            GroupId = link.GroupId,
            Type = link.Type,
            TaskId = link.TaskId,
            ProcessInstanceId = link.ProcessInstanceId,
            ProcessDefinitionId = link.ProcessDefinitionId
        };
    }

    private static void AddCandidateLinks(List<WorkflowIdentityLinkResponse> links, TaskImplementation task)
    {
        foreach (var userId in task.CandidateUsers ?? [])
        {
            if (links.Any(link =>
                    string.Equals(link.Type, IdentityLinkType.CANDIDATE, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(link.UserId, userId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            links.Add(new WorkflowIdentityLinkResponse(
                $"{task.Id}:candidate:user:{userId}",
                userId,
                null,
                IdentityLinkType.CANDIDATE,
                task.Id,
                task.ProcessInstanceId,
                task.ProcessDefinitionId));
        }

        foreach (var groupId in task.CandidateGroups ?? [])
        {
            if (links.Any(link =>
                    string.Equals(link.Type, IdentityLinkType.CANDIDATE, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(link.GroupId, groupId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            links.Add(new WorkflowIdentityLinkResponse(
                $"{task.Id}:candidate:group:{groupId}",
                null,
                groupId,
                IdentityLinkType.CANDIDATE,
                task.Id,
                task.ProcessInstanceId,
                task.ProcessDefinitionId));
        }
    }

    private static WorkflowCommentResponse MapComment(CommentEntity comment)
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

    private static WorkflowAttachmentResponse MapAttachment(AttachmentEntity attachment)
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
}
