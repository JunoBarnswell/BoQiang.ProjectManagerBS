using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Services;
using SqlSugar;
using Volo.Abp.Timing;
using PersistenceHistoricIdentityLinkEntity = AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity;
using PersistenceHistoricTaskInstanceEntity = AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity;
using PersistenceIdentityLinkEntity = AsterERP.Workflow.Persistence.Entities.IdentityLinkEntity;
using PersistenceProcessDefinitionEntity = AsterERP.Workflow.Persistence.Entities.ProcessDefinitionEntity;
using PersistenceTaskEntity = AsterERP.Workflow.Persistence.Entities.TaskEntity;
using TaskImplementation = AsterERP.Workflow.Core.Services.TaskImplementation;
using WorkflowBusinessInstanceEntity = AsterERP.Api.Modules.Workflows.WorkflowBusinessInstanceEntity;
using WorkflowTaskListItemResponse = AsterERP.Contracts.Workflows.WorkflowTaskListItemResponse;
using WorkflowIdentityLinkEntity = AsterERP.Workflow.Core.Cmd.IdentityLinkEntity;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowTaskVisibilityService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IWorkflowIdentityCandidateScope candidateScope,
    IClock clock)
{
    public ISugarQueryable<PersistenceTaskEntity> BuildVisibleRuntimeTaskQuery(
        IReadOnlyCollection<string> candidateTaskIds,
        IReadOnlyCollection<string> delegatedTaskIds)
    {
        var currentUserId = candidateScope.CurrentUserId;
        var taskQuery = databaseAccessor.GetCurrentDb().Queryable<PersistenceTaskEntity>();
        return taskQuery.Where(item =>
            item.Assignee == currentUserId ||
            (candidateTaskIds.Count > 0 && item.Assignee == null && candidateTaskIds.Contains(item.Id)) ||
            (delegatedTaskIds.Count > 0 && delegatedTaskIds.Contains(item.Id)));
    }

    public async Task<IReadOnlyCollection<string>> GetCandidateTaskIdsAsync(CancellationToken cancellationToken)
    {
        var currentUserId = candidateScope.CurrentUserId;
        var groupIds = candidateScope.CandidateGroupIds;
        var taskIds = await databaseAccessor.GetCurrentDb().Queryable<PersistenceIdentityLinkEntity>()
            .Where(item =>
                item.TaskId != null &&
                item.Type == IdentityLinkType.CANDIDATE &&
                (item.UserId == currentUserId || (item.GroupId != null && groupIds.Contains(item.GroupId))))
            .Select(item => item.TaskId)
            .ToListAsync(cancellationToken);

        return NormalizeIds(taskIds);
    }

    public async Task<int> CountCcInstancesAsync(CancellationToken cancellationToken)
    {
        return await BuildCcBusinessInstanceQuery().CountAsync(cancellationToken);
    }

    public ISugarQueryable<WorkflowBusinessInstanceEntity> BuildCcBusinessInstanceQuery()
    {
        var userId = candidateScope.CurrentUserId;
        var groupIds = candidateScope.CandidateGroupIds;
        var hasGroups = groupIds.Count > 0;
        var query = databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>().Where(item => !item.IsDeleted);

        return hasGroups
            ? query.Where(item =>
                SqlFunc.Subqueryable<PersistenceIdentityLinkEntity>()
                    .Where(link =>
                        link.ProcessInstanceId == item.ProcessInstanceId &&
                        (link.Type == "cc" || link.Type == "copy" || link.Type == "copyTo" || link.Type == "carbonCopy") &&
                        (link.UserId == userId || (link.GroupId != null && groupIds.Contains(link.GroupId))))
                    .Any() ||
                SqlFunc.Subqueryable<PersistenceHistoricIdentityLinkEntity>()
                    .Where(link =>
                        link.ProcessInstanceId == item.ProcessInstanceId &&
                        (link.Type == "cc" || link.Type == "copy" || link.Type == "copyTo" || link.Type == "carbonCopy") &&
                        (link.UserId == userId || (link.GroupId != null && groupIds.Contains(link.GroupId))))
                    .Any())
            : query.Where(item =>
                SqlFunc.Subqueryable<PersistenceIdentityLinkEntity>()
                    .Where(link =>
                        link.ProcessInstanceId == item.ProcessInstanceId &&
                        (link.Type == "cc" || link.Type == "copy" || link.Type == "copyTo" || link.Type == "carbonCopy") &&
                        link.UserId == userId)
                    .Any() ||
                SqlFunc.Subqueryable<PersistenceHistoricIdentityLinkEntity>()
                    .Where(link =>
                        link.ProcessInstanceId == item.ProcessInstanceId &&
                        (link.Type == "cc" || link.Type == "copy" || link.Type == "copyTo" || link.Type == "carbonCopy") &&
                        link.UserId == userId)
                    .Any());
    }

    public bool IsRuntimeTaskVisibleToCurrentUser(TaskImplementation task, IReadOnlyCollection<WorkflowIdentityLinkEntity> links)
    {
        if (candidateScope.IsCandidateUser(task.Assignee) ||
            candidateScope.IsCandidateUser(task.Owner))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(task.Assignee))
        {
            return false;
        }

        return (task.CandidateUsers?.Any(candidateScope.IsCandidateUser) ?? false) ||
               (task.CandidateGroups?.Any(candidateScope.IsCandidateGroup) ?? false) ||
               links.Any(link => IsCandidateLinkVisibleToCurrentUser(link, candidateScope.CandidateGroupIds));
    }

    public async Task<bool> IsRuntimeTaskDelegatedToCurrentUserAsync(TaskImplementation task, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(task.Assignee) && string.IsNullOrWhiteSpace(task.Owner))
        {
            return false;
        }

        var delegatedTaskIds = await GetDelegatedTaskIdsAsync([task.Id], cancellationToken);
        return delegatedTaskIds.Contains(task.Id, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyCollection<string>> GetDelegatedTaskIdsAsync(CancellationToken cancellationToken)
    {
        return await GetDelegatedTaskIdsAsync(null, cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetDelegatedTaskIdsAsync(
        IReadOnlyCollection<string>? taskIds,
        CancellationToken cancellationToken)
    {
        var rules = await GetActiveDelegationRulesAsync(cancellationToken);
        if (rules.Count == 0)
        {
            return [];
        }

        var ownerIds = rules.Select(item => item.OwnerUserId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var tasks = await databaseAccessor.GetCurrentDb().Queryable<PersistenceTaskEntity>()
            .Where(item => ownerIds.Contains(item.Assignee!) || ownerIds.Contains(item.Owner!))
            .WhereIF(taskIds is { Count: > 0 }, item => taskIds!.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (tasks.Count == 0)
        {
            return [];
        }

        var definitionIds = tasks
            .Select(item => item.ProcessDefinitionId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var processKeysByDefinitionId = definitionIds.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : (await databaseAccessor.GetCurrentDb().Queryable<PersistenceProcessDefinitionEntity>()
                .Where(item => definitionIds.Contains(item.Id))
                .ToListAsync(cancellationToken))
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(item => item.Id, item => item.Key!, StringComparer.OrdinalIgnoreCase);

        return tasks
            .Where(task => IsTaskMatchedByDelegationRule(task, rules, processKeysByDefinitionId))
            .Select(task => task.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<bool> IsProcessInstanceVisibleToCurrentUserAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        var currentUserId = candidateScope.CurrentUserId;
        if (await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
                .Where(item => !item.IsDeleted && item.ProcessInstanceId == processInstanceId && item.StartedBy == currentUserId)
                .AnyAsync(cancellationToken))
        {
            return true;
        }

        if (await databaseAccessor.GetCurrentDb().Queryable<PersistenceTaskEntity>()
                .Where(item =>
                    item.ProcessInstanceId == processInstanceId &&
                    (item.Assignee == currentUserId || item.Owner == currentUserId))
                .AnyAsync(cancellationToken))
        {
            return true;
        }

        var delegatedTaskIds = await GetDelegatedTaskIdsAsync(cancellationToken);
        if (delegatedTaskIds.Count > 0 &&
            await databaseAccessor.GetCurrentDb().Queryable<PersistenceTaskEntity>()
                .Where(item => item.ProcessInstanceId == processInstanceId && delegatedTaskIds.Contains(item.Id))
                .AnyAsync(cancellationToken))
        {
            return true;
        }

        if (await HasVisibleRuntimeIdentityLinkAsync(processInstanceId, currentUserId, cancellationToken))
        {
            return true;
        }

        if (await databaseAccessor.GetCurrentDb().Queryable<PersistenceHistoricTaskInstanceEntity>()
                .Where(item =>
                    item.ProcessInstanceId == processInstanceId &&
                    (item.Assignee == currentUserId || item.Owner == currentUserId))
                .AnyAsync(cancellationToken))
        {
            return true;
        }

        return await HasVisibleHistoricIdentityLinkAsync(processInstanceId, currentUserId, cancellationToken);
    }

    public bool IsTaskVisibleToCurrentUser(WorkflowTaskListItemResponse task)
    {
        if (candidateScope.IsCandidateUser(task.Assignee))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(task.Assignee))
        {
            return false;
        }

        var groupIds = candidateScope.CandidateGroupIds;
        return task.IdentityLinks.Any(link =>
            string.Equals(link.Type, IdentityLinkType.CANDIDATE, StringComparison.OrdinalIgnoreCase) &&
            IsIdentityLinkVisibleToCurrentUser(link.UserId, link.GroupId, groupIds));
    }

    private bool IsCandidateLinkVisibleToCurrentUser(WorkflowIdentityLinkEntity link, IReadOnlyList<string> groupIds)
    {
        return string.Equals(link.Type, IdentityLinkType.CANDIDATE, StringComparison.OrdinalIgnoreCase) &&
               IsIdentityLinkVisibleToCurrentUser(link.UserId, link.GroupId, groupIds);
    }

    private bool IsIdentityLinkVisibleToCurrentUser(string? userId, string? groupId, IReadOnlyList<string> groupIds)
    {
        return candidateScope.IsCandidateUser(userId) ||
               (!string.IsNullOrWhiteSpace(groupId) && groupIds.Contains(groupId, StringComparer.OrdinalIgnoreCase));
    }

    private Task<bool> HasVisibleRuntimeIdentityLinkAsync(string processInstanceId, string currentUserId, CancellationToken cancellationToken)
    {
        var groupIds = candidateScope.CandidateGroupIds;
        var query = databaseAccessor.GetCurrentDb().Queryable<PersistenceIdentityLinkEntity>()
            .Where(item =>
                item.ProcessInstanceId == processInstanceId ||
                (item.TaskId != null &&
                 SqlFunc.Subqueryable<PersistenceTaskEntity>()
                     .Where(task => task.ProcessInstanceId == processInstanceId && task.Id == item.TaskId)
                     .Any()));

        return groupIds.Count == 0
            ? query.Where(item => item.UserId == currentUserId).AnyAsync(cancellationToken)
            : query.Where(item => item.UserId == currentUserId || (item.GroupId != null && groupIds.Contains(item.GroupId)))
                .AnyAsync(cancellationToken);
    }

    private Task<bool> HasVisibleHistoricIdentityLinkAsync(string processInstanceId, string currentUserId, CancellationToken cancellationToken)
    {
        var groupIds = candidateScope.CandidateGroupIds;
        var query = databaseAccessor.GetCurrentDb().Queryable<PersistenceHistoricIdentityLinkEntity>()
            .Where(item => item.ProcessInstanceId == processInstanceId);

        return groupIds.Count == 0
            ? query.Where(item => item.UserId == currentUserId).AnyAsync(cancellationToken)
            : query.Where(item => item.UserId == currentUserId || (item.GroupId != null && groupIds.Contains(item.GroupId)))
                .AnyAsync(cancellationToken);
    }

    private static IReadOnlyCollection<string> NormalizeIds(IEnumerable<string?> ids) =>
        ids.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private async Task<List<WorkflowDelegationRuleEntity>> GetActiveDelegationRulesAsync(CancellationToken cancellationToken)
    {
        var now = clock.Now;
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowDelegationRuleEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.IsEnabled &&
                item.DelegateUserId == candidateScope.CurrentUserId &&
                item.StartAt <= now &&
                item.EndAt > now)
            .ToListAsync(cancellationToken);
    }

    private static bool IsTaskMatchedByDelegationRule(
        PersistenceTaskEntity task,
        IReadOnlyList<WorkflowDelegationRuleEntity> rules,
        IReadOnlyDictionary<string, string> processKeysByDefinitionId)
    {
        var taskOwnerIds = new[] { task.Assignee, task.Owner }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (taskOwnerIds.Count == 0)
        {
            return false;
        }

        var processDefinitionKey = string.IsNullOrWhiteSpace(task.ProcessDefinitionId)
            ? null
            : processKeysByDefinitionId.GetValueOrDefault(task.ProcessDefinitionId);
        return rules.Any(rule =>
            taskOwnerIds.Contains(rule.OwnerUserId) &&
            (string.Equals(rule.ScopeType, "All", StringComparison.OrdinalIgnoreCase) ||
             (!string.IsNullOrWhiteSpace(processDefinitionKey) &&
              string.Equals(rule.ProcessDefinitionKey, processDefinitionKey, StringComparison.OrdinalIgnoreCase))));
    }
}

