using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;
using CoreCommentEntity = AsterERP.Workflow.Core.Cmd.CommentEntity;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

public sealed class SqlSugarApprovalQueryService : IApprovalQueryService
{
    private const string CandidateType = "candidate";

    private readonly ISqlSugarClient _db;
    private readonly IWorkflowPersistenceStore _store;

    public SqlSugarApprovalQueryService(ISqlSugarClient db, IWorkflowPersistenceStore store)
    {
        _db = db;
        _store = store;
    }

    public async Task<List<ApprovalTaskView>> GetPendingTasksForAssigneeAsync(string assignee, CancellationToken cancellationToken = default)
    {
        var tasks = await _db.Queryable<TaskEntity>()
            .Where(it => it.Assignee == assignee)
            .OrderBy(it => it.CreateTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return await MapTaskViewsAsync(tasks, cancellationToken);
    }

    public async Task<List<ApprovalTaskView>> GetCandidateTasksForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var taskIds = await _db.Queryable<IdentityLinkEntity>()
            .Where(it => it.Type == CandidateType && it.UserId == userId)
            .Select(it => it.TaskId!)
            .ToListAsync(cancellationToken);
        return await LoadUnclaimedTaskViewsAsync(taskIds, userId, null, cancellationToken);
    }

    public async Task<List<ApprovalTaskView>> GetCandidateTasksForGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var taskIds = await _db.Queryable<IdentityLinkEntity>()
            .Where(it => it.Type == CandidateType && it.GroupId == groupId)
            .Select(it => it.TaskId!)
            .ToListAsync(cancellationToken);
        return await LoadUnclaimedTaskViewsAsync(taskIds, null, groupId, cancellationToken);
    }

    public async Task<List<ApprovalTaskView>> GetCompletedTasksForAssigneeAsync(string assignee, CancellationToken cancellationToken = default)
    {
        var tasks = await _db.Queryable<HistoricTaskInstanceEntity>()
            .Where(it => it.Assignee == assignee && it.EndTime != null)
            .OrderBy(it => it.EndTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        var processBusinessKeys = await LoadBusinessKeysAsync(tasks.Select(task => task.ProcessInstanceId).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().Distinct().ToList(), cancellationToken);
        return tasks.Select(task => new ApprovalTaskView
        {
            TaskId = task.Id,
            TaskName = task.Name,
            ProcessInstanceId = task.ProcessInstanceId,
            ProcessDefinitionId = task.ProcessDefinitionId,
            Assignee = task.Assignee,
            Owner = task.Owner,
            FormKey = task.FormKey,
            BusinessKey = task.ProcessInstanceId != null && processBusinessKeys.TryGetValue(task.ProcessInstanceId, out var businessKey) ? businessKey : null,
            CreateTime = task.StartTime,
            DueDate = task.DueDate,
            TaskDefinitionKey = task.TaskDefinitionKey
        }).ToList();
    }

    public async Task<List<ApprovalProcessView>> GetStartedProcessesAsync(string initiator, CancellationToken cancellationToken = default)
    {
        var processes = await _db.Queryable<HistoricProcessInstanceEntity>()
            .Where(it => it.StartUserId == initiator)
            .OrderBy(it => it.StartTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return processes.Select(MapProcessView).ToList();
    }

    public async Task<List<ApprovalProcessView>> GetCompletedProcessesAsync(string? businessKey = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<HistoricProcessInstanceEntity>()
            .Where(it => it.EndTime != null);
        if (!string.IsNullOrWhiteSpace(businessKey))
        {
            query = query.Where(it => it.BusinessKey == businessKey);
        }

        var processes = await query
            .OrderBy(it => it.EndTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return processes.Select(MapProcessView).ToList();
    }

    public async Task<ApprovalHistoryReport> GetApprovalHistoryAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var process = await _db.Queryable<HistoricProcessInstanceEntity>().InSingleAsync(processInstanceId);
        var historicTasks = await _db.Queryable<HistoricTaskInstanceEntity>()
            .Where(it => it.ProcessInstanceId == processInstanceId)
            .OrderBy(it => it.StartTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var activities = await _db.Queryable<HistoricActivityInstanceEntity>()
            .Where(it => it.ProcessInstanceId == processInstanceId)
            .OrderBy(it => it.StartTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var comments = await _store.GetProcessInstanceCommentsAsync(processInstanceId, null, cancellationToken);
        var detailMap = (await _store.GetHistoricDetailsAsync(processInstanceId, cancellationToken))
            .GroupBy(detail => detail.TaskId ?? detail.VariableName ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, object?>)group
                    .Where(detail => !string.IsNullOrWhiteSpace(detail.VariableName))
                    .GroupBy(detail => detail.VariableName!, StringComparer.Ordinal)
                    .ToDictionary(item => item.Key, item => item.Last().VariableValue, StringComparer.Ordinal),
                StringComparer.Ordinal);

        var entries = new List<ApprovalHistoryEntry>();
        entries.AddRange(activities.Select(activity => new ApprovalHistoryEntry
        {
            EntryType = "activity",
            ActivityId = activity.ActivityId,
            ActivityName = activity.ActivityName,
            Time = activity.EndTime ?? activity.StartTime
        }));
        entries.AddRange(historicTasks.Select(task => new ApprovalHistoryEntry
        {
            EntryType = "task",
            TaskId = task.Id,
            ActivityId = task.TaskDefinitionKey,
            ActivityName = task.Name,
            Assignee = task.Assignee,
            Time = task.EndTime ?? task.StartTime,
            Variables = detailMap.TryGetValue(task.Id, out var variables) ? variables : new Dictionary<string, object?>()
        }));
        entries.AddRange(comments.Select(comment => new ApprovalHistoryEntry
        {
            EntryType = string.Equals(comment.Type, CoreCommentEntity.TYPE_EVENT, StringComparison.OrdinalIgnoreCase) ? "event" : "comment",
            TaskId = comment.TaskId,
            Assignee = comment.UserId,
            Message = comment.Message,
            Action = comment.Action,
            Time = comment.Time
        }));

        return new ApprovalHistoryReport
        {
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionId = process?.ProcessDefinitionId,
            BusinessKey = process?.BusinessKey,
            StartUserId = process?.StartUserId,
            Entries = entries.OrderBy(entry => entry.Time).ToList()
        };
    }

    private async Task<List<ApprovalTaskView>> LoadUnclaimedTaskViewsAsync(
        List<string> taskIds,
        string? candidateUser,
        string? candidateGroup,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return new List<ApprovalTaskView>();
        }

        var tasks = await _db.Queryable<TaskEntity>()
            .Where(it => taskIds.Contains(it.Id) && it.Assignee == null)
            .OrderBy(it => it.CreateTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        var views = await MapTaskViewsAsync(tasks, cancellationToken);
        return views.Select(view => view with { CandidateUser = candidateUser, CandidateGroup = candidateGroup }).ToList();
    }

    private async Task<List<ApprovalTaskView>> MapTaskViewsAsync(List<TaskEntity> tasks, CancellationToken cancellationToken)
    {
        if (tasks.Count == 0)
        {
            return new List<ApprovalTaskView>();
        }

        var processBusinessKeys = await LoadBusinessKeysAsync(tasks.Select(task => task.ProcessInstanceId).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().Distinct().ToList(), cancellationToken);
        return tasks.Select(task => new ApprovalTaskView
        {
            TaskId = task.Id,
            TaskName = task.Name,
            ProcessInstanceId = task.ProcessInstanceId,
            ProcessDefinitionId = task.ProcessDefinitionId,
            Assignee = task.Assignee,
            Owner = task.Owner,
            FormKey = task.FormKey,
            BusinessKey = task.ProcessInstanceId != null && processBusinessKeys.TryGetValue(task.ProcessInstanceId, out var businessKey) ? businessKey : null,
            CreateTime = task.CreateTime,
            DueDate = task.DueDate,
            TaskDefinitionKey = task.TaskDefinitionKey
        }).ToList();
    }

    private async Task<Dictionary<string, string?>> LoadBusinessKeysAsync(List<string> processInstanceIds, CancellationToken cancellationToken)
    {
        if (processInstanceIds.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        var processes = await _db.Queryable<HistoricProcessInstanceEntity>()
            .Where(it => processInstanceIds.Contains(it.Id))
            .ToListAsync(cancellationToken);
        return processes.ToDictionary(process => process.Id, process => process.BusinessKey, StringComparer.Ordinal);
    }

    private static ApprovalProcessView MapProcessView(HistoricProcessInstanceEntity process)
    {
        return new ApprovalProcessView
        {
            ProcessInstanceId = process.Id,
            ProcessDefinitionId = process.ProcessDefinitionId,
            BusinessKey = process.BusinessKey,
            StartUserId = process.StartUserId,
            StartTime = process.StartTime,
            EndTime = process.EndTime,
            IsCompleted = process.EndTime != null
        };
    }
}
