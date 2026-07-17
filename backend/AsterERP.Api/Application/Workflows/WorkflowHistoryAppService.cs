using AsterERP.Contracts.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowHistoryAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IHistoryService historyService) : IWorkflowHistoryAppService
{
    public async Task<GridPageResult<WorkflowHistoricProcessResponse>> GetProcessesAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<HistoricProcessInstanceEntity>()
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.Id.Contains(query.Keyword!) || item.BusinessKey!.Contains(query.Keyword!) || item.ProcessDefinitionId!.Contains(query.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(query.TenantId), item => item.TenantId == query.TenantId)
            .OrderBy(item => item.StartTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        var mapped = items.Select(item => new WorkflowHistoricProcessResponse(
            item.Id,
            item.ProcessDefinitionId,
            item.BusinessKey,
            item.StartUserId,
            item.StartTime,
            item.EndTime,
            item.DurationInMillis,
            item.DeleteReason)).ToList();

        return new GridPageResult<WorkflowHistoricProcessResponse>
        {
            Total = total.Value,
            Items = await EnrichHistoricProcessesAsync(mapped, cancellationToken)
        };
    }

    public async Task<GridPageResult<WorkflowHistoricTaskResponse>> GetTasksAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<HistoricTaskInstanceEntity>()
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.Name!.Contains(query.Keyword!) || item.ProcessInstanceId!.Contains(query.Keyword!) || item.Assignee!.Contains(query.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status) && query.Status == "Finished", item => item.EndTime != null)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status) && query.Status == "Running", item => item.EndTime == null)
            .OrderBy(item => item.StartTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        var mapped = items.Select(item => new WorkflowHistoricTaskResponse(
            item.Id,
            item.Name,
            item.Assignee,
            item.Owner,
            item.ProcessInstanceId,
            item.TaskDefinitionKey,
            item.StartTime,
            item.EndTime,
            item.DurationInMillis,
            item.DeleteReason)
        {
            ProcessDefinitionId = item.ProcessDefinitionId
        }).ToList();

        return new GridPageResult<WorkflowHistoricTaskResponse>
        {
            Total = total.Value,
            Items = await EnrichHistoricTasksAsync(mapped, cancellationToken)
        };
    }

    public async Task<GridPageResult<WorkflowActivityResponse>> GetActivitiesAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<HistoricActivityInstanceEntity>()
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.ActivityName!.Contains(query.Keyword!) || item.ActivityId!.Contains(query.Keyword!) || item.ProcessInstanceId!.Contains(query.Keyword!))
            .OrderBy(item => item.StartTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return new GridPageResult<WorkflowActivityResponse>
        {
            Total = total.Value,
            Items = items.Select(item => new WorkflowActivityResponse(
                item.Id,
                item.ActivityId,
                item.ActivityName,
                item.ActivityType,
                item.ExecutionId,
                item.ProcessInstanceId,
                item.StartTime,
                item.EndTime,
                item.DurationInMillis)).ToList()
        };
    }

    public async Task<GridPageResult<WorkflowHistoricVariableResponse>> GetVariablesAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<HistoricVariableInstanceEntity>()
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.Name!.Contains(query.Keyword!) || item.ProcessInstanceId!.Contains(query.Keyword!))
            .OrderBy(item => item.CreateTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return new GridPageResult<WorkflowHistoricVariableResponse>
        {
            Total = total.Value,
            Items = items
                .Select(item => new WorkflowHistoricVariableResponse(
                    item.Id,
                    item.Name,
                    item.Type,
                    ResolveVariableValue(item),
                    item.ProcessInstanceId,
                    item.TaskId,
                    item.CreateTime,
                    item.LastUpdatedTime))
                .ToList()
        };
    }

    public async Task<IReadOnlyList<WorkflowIdentityLinkResponse>> GetIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var links = await historyService.GetHistoricIdentityLinksForProcessInstanceAsync(processInstanceId, cancellationToken);
        return links.Select(item => new WorkflowIdentityLinkResponse(
            item.Id,
            item.UserId,
            item.GroupId,
            item.Type,
            item.TaskId,
            item.ProcessInstanceId,
            item.ProcessDefinitionId)).ToList();
    }

    private async Task<List<WorkflowHistoricProcessResponse>> EnrichHistoricProcessesAsync(
        IReadOnlyList<WorkflowHistoricProcessResponse> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var processInstanceIds = NormalizeIds(items.Select(item => item.Id));
        var instances = await GetBusinessInstanceMapAsync(processInstanceIds, cancellationToken);
        var processDefinitions = await GetProcessDefinitionNamesAsync(
            NormalizeIds(items.Select(item => item.ProcessDefinitionId).Concat(instances.Values.Select(item => item.ProcessDefinitionId))),
            cancellationToken);
        var userNames = await GetUserDisplayNamesAsync(items.Select(item => item.StartUserId).Concat(instances.Values.Select(item => item.StartedBy)), cancellationToken);

        return items.Select(item =>
        {
            instances.TryGetValue(item.Id, out var instance);
            return item with
            {
                BusinessType = instance?.BusinessType,
                ProcessName = ResolveProcessName(item.ProcessDefinitionId, instance, processDefinitions),
                StarterUserName = ResolveUserName(instance?.StartedBy ?? item.StartUserId, userNames),
                Status = instance?.Status ?? (item.EndTime.HasValue ? "Completed" : "Running")
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
        var processDefinitions = await GetProcessDefinitionNamesAsync(
            NormalizeIds(items.Select(item => item.ProcessDefinitionId).Concat(instances.Values.Select(item => item.ProcessDefinitionId))),
            cancellationToken);
        var userNames = await GetUserDisplayNamesAsync(items.SelectMany(item => new[] { item.Assignee, item.Owner }).Concat(instances.Values.Select(item => item.StartedBy)), cancellationToken);
        var commentCounts = await GetCommentCountMapAsync(taskIds, processInstanceIds, cancellationToken);
        var attachmentCounts = await GetAttachmentCountMapAsync(taskIds, processInstanceIds, cancellationToken);

        return items.Select(item =>
        {
            instances.TryGetValue(item.ProcessInstanceId ?? string.Empty, out var instance);
            return item with
            {
                BusinessType = instance?.BusinessType,
                BusinessKey = instance?.BusinessKey,
                ProcessName = ResolveProcessName(item.ProcessDefinitionId, instance, processDefinitions),
                StarterUserName = ResolveUserName(instance?.StartedBy, userNames),
                AssigneeName = ResolveUserName(item.Assignee, userNames),
                CommentsCount = CountForTaskAndProcess(commentCounts, item.Id, item.ProcessInstanceId),
                AttachmentsCount = CountForTaskAndProcess(attachmentCounts, item.Id, item.ProcessInstanceId)
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
        IReadOnlyCollection<string> processDefinitionIds,
        CancellationToken cancellationToken)
    {
        if (processDefinitionIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var definitions = await databaseAccessor.GetCurrentDb().Queryable<ProcessDefinitionEntity>()
            .Where(item => processDefinitionIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        return definitions.ToDictionary(
            item => item.Id,
            item => string.IsNullOrWhiteSpace(item.Name) ? item.Key ?? item.Id : item.Name!,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, string>> GetUserDisplayNamesAsync(IEnumerable<string?> userIds, CancellationToken cancellationToken)
    {
        var ids = NormalizeIds(userIds);
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && (ids.Contains(item.Id) || ids.Contains(item.UserName)))
            .ToListAsync(cancellationToken);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
            result[user.Id] = displayName;
            result[user.UserName] = displayName;
        }

        var missingIds = ids.Where(item => !result.ContainsKey(item)).ToList();
        if (missingIds.Count > 0)
        {
            var identityUsers = await databaseAccessor.GetCurrentDb().Queryable<ActIdUserEntity>()
                .Where(item => missingIds.Contains(item.Id) || (item.LastName != null && missingIds.Contains(item.LastName)))
                .ToListAsync(cancellationToken);
            foreach (var identityUser in identityUsers)
            {
                var displayName = !string.IsNullOrWhiteSpace(identityUser.DisplayName)
                    ? identityUser.DisplayName!
                    : !string.IsNullOrWhiteSpace(identityUser.FirstName)
                        ? identityUser.FirstName!
                        : !string.IsNullOrWhiteSpace(identityUser.LastName)
                            ? identityUser.LastName!
                            : identityUser.Id;
                result[identityUser.Id] = displayName;
                if (!string.IsNullOrWhiteSpace(identityUser.LastName))
                {
                    result[identityUser.LastName!] = displayName;
                }
            }
        }

        return result;
    }

    private async Task<Dictionary<string, int>> GetCommentCountMapAsync(
        IReadOnlyCollection<string> taskIds,
        IReadOnlyCollection<string> processInstanceIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0 && processInstanceIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await databaseAccessor.GetCurrentDb().Queryable<CommentEntity>()
            .Where(item =>
                (item.TaskId != null && taskIds.Contains(item.TaskId)) ||
                (item.ProcessInstanceId != null && processInstanceIds.Contains(item.ProcessInstanceId)))
            .ToListAsync(cancellationToken);
        return BuildTaskProcessCountMap(rows.Select(item => (item.TaskId, item.ProcessInstanceId)));
    }

    private async Task<Dictionary<string, int>> GetAttachmentCountMapAsync(
        IReadOnlyCollection<string> taskIds,
        IReadOnlyCollection<string> processInstanceIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0 && processInstanceIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await databaseAccessor.GetCurrentDb().Queryable<AttachmentEntity>()
            .Where(item =>
                (item.TaskId != null && taskIds.Contains(item.TaskId)) ||
                (item.ProcessInstanceId != null && processInstanceIds.Contains(item.ProcessInstanceId)))
            .ToListAsync(cancellationToken);
        return BuildTaskProcessCountMap(rows.Select(item => (item.TaskId, item.ProcessInstanceId)));
    }

    private static IReadOnlyCollection<string> NormalizeIds(IEnumerable<string?> ids)
    {
        return ids
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static string? ResolveUserName(string? userId, IReadOnlyDictionary<string, string> userNames)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return userNames.TryGetValue(userId, out var name) ? name : userId;
    }

    private static object? ResolveVariableValue(HistoricVariableInstanceEntity item)
    {
        if (item.DoubleValue.HasValue)
        {
            return item.DoubleValue;
        }

        if (item.LongValue.HasValue)
        {
            return item.LongValue;
        }

        return item.TextValue ?? item.TextValue2 ?? item.ByteArrayId;
    }

    private static Dictionary<string, int> BuildTaskProcessCountMap(IEnumerable<(string? TaskId, string? ProcessInstanceId)> items)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (taskId, processInstanceId) in items)
        {
            IncrementCount(result, CountKey("task", taskId));
            IncrementCount(result, CountKey("process", processInstanceId));
        }

        return result;
    }

    private static int CountForTaskAndProcess(
        IReadOnlyDictionary<string, int> counts,
        string taskId,
        string? processInstanceId)
    {
        return GetCount(counts, CountKey("task", taskId)) +
               GetCount(counts, CountKey("process", processInstanceId));
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

    private static string? CountKey(string scope, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"{scope}:{value}";
    }
}

