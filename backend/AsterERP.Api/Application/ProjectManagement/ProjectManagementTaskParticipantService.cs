using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskParticipantService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementMemberCandidateService candidateService,
    IProjectManagementImConversationService? imConversationService = null,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementNotificationPublisher? notificationPublisher = null,
    ProjectManagementTaskParticipantProjection? projection = null) : IProjectManagementTaskParticipantService
{
    public async Task<IReadOnlyList<ProjectManagementTaskParticipantResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        return (await Projection.LoadByTaskIdsAsync([task.Id], includeHistorical: false, cancellationToken))
            .GetValueOrDefault(task.Id, []);
    }

    public async Task<IReadOnlyList<ProjectManagementTaskParticipantResponse>> QueryHistoryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        return (await Projection.LoadByTaskIdsAsync([task.Id], includeHistorical: true, cancellationToken))
            .GetValueOrDefault(task.Id, []);
    }

    public async Task<GridPageResult<ProjectManagementTaskParticipantCandidateResponse>> QueryCandidatesAsync(string taskId, ProjectManagementTaskParticipantCandidateQuery query, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        return await Projection.QueryCandidatesAsync(task.ProjectId, query, cancellationToken);
    }

    public async Task<ProjectManagementTaskParticipantBatchMutationResult> ReplaceParticipantsForTasksAsync(
        ISqlSugarClient db,
        ProjectManagementTaskParticipantBatchReplaceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        var projectId = Required(request.ProjectId, "项目不能为空");
        if (request.Items is null || request.Items.Count == 0 || request.Items.Count > 200)
            throw new ValidationException("批量参与人任务数量必须在 1 到 200 之间");

        var replacements = NormalizeBatchItems(request.Items);
        var taskIds = replacements.Select(item => item.TaskId).ToList();
        var tasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => taskIds.Contains(item.Id) && item.ProjectId == projectId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (tasks.Count != taskIds.Count)
            throw new ValidationException("存在不属于当前项目、当前租户或已删除的任务");
        var tasksById = tasks.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var desiredByTaskId = replacements.ToDictionary(item => item.TaskId, item => item.ParticipantUserIds.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var desiredUserIds = desiredByTaskId.Values.SelectMany(item => item).Distinct(StringComparer.Ordinal).ToList();
        var selectableUserIds = await Projection.LoadSelectableUserIdsAsync(desiredUserIds, cancellationToken);
        var invalidUserIds = desiredUserIds.Where(userId => !selectableUserIds.Contains(userId)).ToList();
        if (invalidUserIds.Count > 0)
            throw new ValidationException($"参与人必须来自当前租户和应用的启用用户/任职: {string.Join("、", invalidUserIds)}");

        var members = desiredUserIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementProjectMemberEntity>()
                .Where(item => item.ProjectId == projectId && desiredUserIds.Contains(item.UserId) && item.TenantId == Tenant() && item.AppCode == App() && item.IsActive && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var membersByUserId = members.ToDictionary(item => item.UserId, StringComparer.Ordinal);
        var nonMemberUserIds = desiredUserIds.Where(userId => !membersByUserId.ContainsKey(userId)).ToList();
        if (nonMemberUserIds.Count > 0)
            throw new ValidationException($"参与人必须是当前项目的启用成员: {string.Join("、", nonMemberUserIds)}");
        await EnsureMemberScopesAsync(db, tasks, desiredByTaskId, membersByUserId, cancellationToken);

        var historicalParticipants = await db.Queryable<ProjectManagementTaskParticipantEntity>()
            .Where(item => taskIds.Contains(item.TaskId) && item.ProjectId == projectId && item.TenantId == Tenant() && item.AppCode == App())
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        var participantsByTaskAndUser = historicalParticipants
            .GroupBy(item => ParticipantKey(item.TaskId, item.UserId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var traceId = string.IsNullOrWhiteSpace(request.TraceId) ? Guid.NewGuid().ToString("N") : request.TraceId.Trim();
        var added = new List<ProjectManagementTaskParticipantEntity>();
        var updated = new List<ProjectManagementTaskParticipantEntity>();
        var results = new List<ProjectManagementTaskParticipantBatchTaskResult>(tasks.Count);
        var notifications = new List<ProjectManagementTaskParticipantBatchNotification>();
        var conversationTaskIds = new List<string>();

        foreach (var replacement in replacements)
        {
            var task = tasksById[replacement.TaskId];
            var desired = desiredByTaskId[task.Id];
            var current = historicalParticipants
                .Where(item => item.TaskId == task.Id && !item.IsDeleted)
                .ToDictionary(item => item.UserId, StringComparer.Ordinal);
            var addedUserIds = desired.Except(current.Keys, StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
            var removedUserIds = current.Keys.Except(desired, StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
            var unchangedUserIds = desired.Intersect(current.Keys, StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
            if (addedUserIds.Count == 0 && removedUserIds.Count == 0)
            {
                results.Add(new ProjectManagementTaskParticipantBatchTaskResult(task.Id, addedUserIds, removedUserIds, unchangedUserIds));
                continue;
            }

            foreach (var userId in addedUserIds)
            {
                var key = ParticipantKey(task.Id, userId);
                var reusable = participantsByTaskAndUser.TryGetValue(key, out var historical)
                    ? historical.FirstOrDefault(item => item.IsDeleted) ?? historical.First()
                    : null;
                var entity = reusable ?? new ProjectManagementTaskParticipantEntity
                {
                    TenantId = Tenant(), AppCode = App(), ProjectId = projectId, TaskId = task.Id, UserId = userId, CreatedBy = User(), CreatedTime = now, VersionNo = 1
                };
                entity.EmploymentId = membersByUserId[userId].EmploymentId;
                entity.RoleCode = "Participant";
                entity.IsDeleted = false;
                entity.DeletedBy = null;
                entity.DeletedTime = null;
                entity.UpdatedBy = User();
                entity.UpdatedTime = now;
                if (reusable is null) added.Add(entity);
                else { entity.VersionNo++; updated.Add(entity); }
                notifications.Add(CreateBatchNotification(task, userId, "task.participant.added", "你已被加入任务参与人", $"你已被加入任务 {task.Title} 的参与人", traceId));
            }
            foreach (var userId in removedUserIds)
            {
                var entity = current[userId];
                entity.IsDeleted = true;
                entity.DeletedBy = User();
                entity.DeletedTime = now;
                entity.UpdatedBy = entity.DeletedBy;
                entity.UpdatedTime = now;
                entity.VersionNo++;
                updated.Add(entity);
                notifications.Add(CreateBatchNotification(task, userId, "task.participant.removed", "你已被移出任务参与人", $"你已被移出任务 {task.Title} 的参与人", traceId));
            }

            await WriteBatchActivityAsync(task, current.Keys, desired, addedUserIds, removedUserIds, traceId, now, cancellationToken);
            conversationTaskIds.Add(task.Id);
            results.Add(new ProjectManagementTaskParticipantBatchTaskResult(task.Id, addedUserIds, removedUserIds, unchangedUserIds));
        }
        if (added.Count > 0) await db.Insertable(added).ExecuteCommandAsync(cancellationToken);
        if (updated.Count > 0) await db.Updateable(updated).ExecuteCommandAsync(cancellationToken);
        return new ProjectManagementTaskParticipantBatchMutationResult(projectId, traceId, results, notifications, conversationTaskIds);
    }

    public async Task PublishCommittedBatchMutationAsync(ProjectManagementTaskParticipantBatchMutationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (imConversationService is not null)
            foreach (var taskId in result.ConversationSyncTaskIds.Distinct(StringComparer.Ordinal))
                await imConversationService.SynchronizeTaskLinksAsync(taskId, cancellationToken);
        if (notificationPublisher is null) return;
        foreach (var notification in result.Notifications)
            await notificationPublisher.PublishAsync(new ProjectManagementNotification(
                Tenant(), App(), notification.NotificationType, notification.RecipientUserId, notification.Title, notification.Message,
                $"/projects/{notification.ProjectId}/tasks?selectedTaskId={notification.TaskId}", notification.TraceId, notification.ProjectId, notification.TaskId), cancellationToken);
    }

    public async Task<ProjectManagementTaskParticipantResponse> AddAsync(string taskId, ProjectManagementTaskParticipantUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        EnsureVersion(task.VersionNo, request.VersionNo);
        var userId = Required(request.UserId, "参与人不能为空");
        if (!await candidateService.IsSelectableAsync(userId, cancellationToken)) throw new ValidationException("参与人必须来自当前租户和应用的启用用户/任职");
        var db = databaseAccessor.GetCurrentDb();
        var member = (await db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == task.ProjectId && item.UserId == userId && item.TenantId == Tenant() && item.AppCode == App() && item.IsActive && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new ValidationException("参与人必须是当前项目的启用成员");
        await EnsureMemberScopeAsync(task, member, cancellationToken);
        var existing = (await db.Queryable<ProjectManagementTaskParticipantEntity>()
            .Where(item => item.TaskId == task.Id && item.UserId == userId && item.TenantId == Tenant() && item.AppCode == App())
            .OrderBy(item => item.CreatedTime, SqlSugar.OrderByType.Desc)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (existing is { IsDeleted: false }) throw new ValidationException("该参与人已存在");
        var now = DateTime.UtcNow;
        var entity = existing ?? new ProjectManagementTaskParticipantEntity
        {
            TenantId = Tenant(), AppCode = App(), ProjectId = task.ProjectId, TaskId = task.Id, UserId = userId, CreatedBy = User(), CreatedTime = now
        };
        entity.EmploymentId = Optional(request.EmploymentId) ?? member.EmploymentId;
        entity.RoleCode = Required(request.RoleCode, "参与人角色不能为空");
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.VersionNo = existing is null ? 1 : entity.VersionNo + 1;
        entity.UpdatedBy = User();
        entity.UpdatedTime = now;
        var traceId = Guid.NewGuid().ToString("N");
        db.Ado.BeginTran();
        try
        {
            if (existing is null) await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            else await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            task.VersionNo++;
            task.UpdatedBy = User();
            task.UpdatedTime = now;
            await db.Updateable(task).UpdateColumns(item => new { item.VersionNo, item.UpdatedBy, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(task, entity, "task.participant.added", $"将 {userId} 添加为任务参与人", assigned: true, traceId, now, cancellationToken);
            db.Ado.CommitTran();
        }
        catch { db.Ado.RollbackTran(); throw; }
        if (imConversationService is not null)
        {
            await imConversationService.SynchronizeTaskLinksAsync(task.Id, cancellationToken);
        }
        await PublishNotificationAsync(task, userId, "task.participant.added", "你已被加入任务参与人", $"你已被加入任务 {task.Title} 的参与人", traceId, cancellationToken);
        return (await Projection.LoadByTaskIdsAsync([task.Id], includeHistorical: false, cancellationToken)).GetValueOrDefault(task.Id, []).Single(item => item.Id == entity.Id);
    }

    public async Task RemoveAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        EnsureVersion(task.VersionNo, versionNo);
        var db = databaseAccessor.GetCurrentDb();
        var entity = (await db.Queryable<ProjectManagementTaskParticipantEntity>().Where(item => item.Id == id && item.TaskId == task.Id && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务参与人不存在", ErrorCodes.PlatformResourceNotFound);
        var now = DateTime.UtcNow;
        var traceId = Guid.NewGuid().ToString("N");
        entity.IsDeleted = true; entity.DeletedBy = User(); entity.DeletedTime = now; entity.UpdatedBy = entity.DeletedBy; entity.UpdatedTime = now; entity.VersionNo++;
        db.Ado.BeginTran();
        try
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            task.VersionNo++;
            task.UpdatedBy = User();
            task.UpdatedTime = now;
            await db.Updateable(task).UpdateColumns(item => new { item.VersionNo, item.UpdatedBy, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(task, entity, "task.participant.removed", $"将 {entity.UserId} 移出任务参与人", assigned: false, traceId, now, cancellationToken);
            db.Ado.CommitTran();
        }
        catch { db.Ado.RollbackTran(); throw; }
        if (imConversationService is not null)
        {
            await imConversationService.RevokeTaskParticipantAsync(task.Id, entity.UserId, cancellationToken);
        }
        await PublishNotificationAsync(task, entity.UserId, "task.participant.removed", "你已被移出任务参与人", $"你已被移出任务 {task.Title} 的参与人", traceId, cancellationToken);
    }

    private ProjectManagementTaskParticipantProjection Projection => projection ?? new ProjectManagementTaskParticipantProjection(databaseAccessor, currentUser);
    private async Task<ProjectManagementTaskEntity> EnsureTaskAsync(string id, CancellationToken cancellationToken) => (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    private async Task EnsureMemberScopeAsync(ProjectManagementTaskEntity task, ProjectManagementProjectMemberEntity member, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(member.ScopeRootTaskId)) return;
        var scopeRootId = member.ScopeRootTaskId.Trim();
        if (string.Equals(task.Id, scopeRootId, StringComparison.Ordinal)) return;
        var taskAncestors = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == task.ProjectId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var current = taskAncestors.FirstOrDefault(item => item.Id == task.Id);
        while (current?.ParentTaskId is { Length: > 0 })
        {
            if (string.Equals(current.ParentTaskId, scopeRootId, StringComparison.Ordinal)) return;
            current = taskAncestors.FirstOrDefault(item => item.Id == current.ParentTaskId);
        }
        throw new ValidationException("参与人不在该任务的成员授权范围内", ErrorCodes.PermissionDenied);
    }
    private async Task EnsureMemberScopesAsync(
        ISqlSugarClient db,
        IReadOnlyCollection<ProjectManagementTaskEntity> tasks,
        IReadOnlyDictionary<string, HashSet<string>> desiredByTaskId,
        IReadOnlyDictionary<string, ProjectManagementProjectMemberEntity> membersByUserId,
        CancellationToken cancellationToken)
    {
        if (!membersByUserId.Values.Any(member => !string.IsNullOrWhiteSpace(member.ScopeRootTaskId))) return;
        var projectId = tasks.First().ProjectId;
        var taskTree = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var byId = taskTree.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            foreach (var userId in desiredByTaskId[task.Id])
            {
                var scopeRootTaskId = membersByUserId[userId].ScopeRootTaskId;
                if (string.IsNullOrWhiteSpace(scopeRootTaskId)) continue;
                if (!IsWithinScope(task, scopeRootTaskId.Trim(), byId))
                    throw new ValidationException("参与人不在该任务的成员授权范围内", ErrorCodes.PermissionDenied);
            }
        }
    }
    private static bool IsWithinScope(ProjectManagementTaskEntity task, string scopeRootTaskId, IReadOnlyDictionary<string, ProjectManagementTaskEntity> tasksById)
    {
        if (string.Equals(task.Id, scopeRootTaskId, StringComparison.Ordinal)) return true;
        var current = task;
        while (current.ParentTaskId is { Length: > 0 } parentTaskId)
        {
            if (string.Equals(parentTaskId, scopeRootTaskId, StringComparison.Ordinal)) return true;
            if (!tasksById.TryGetValue(parentTaskId, out current!)) return false;
        }
        return false;
    }
    private async Task WriteBatchActivityAsync(ProjectManagementTaskEntity task, IEnumerable<string> beforeUserIds, IEnumerable<string> afterUserIds, IReadOnlyCollection<string> addedUserIds, IReadOnlyCollection<string> removedUserIds, string traceId, DateTime occurredAt, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        var before = FormatUserIds(beforeUserIds);
        var after = FormatUserIds(afterUserIds);
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            Tenant(), App(), "Task", task.Id, "task.participants.replaced", $"批量调整任务参与人：新增 {addedUserIds.Count} 人，移除 {removedUserIds.Count} 人", traceId, User(), task.ProjectId,
            FieldChanges: [new ProjectManagementActivityFieldChange("ParticipantUserIds", "参与人", before, after)],
            OccurredAt: occurredAt), cancellationToken);
    }
    private static ProjectManagementTaskParticipantBatchNotification CreateBatchNotification(ProjectManagementTaskEntity task, string recipientUserId, string notificationType, string title, string message, string traceId) =>
        new(task.Id, task.ProjectId, task.Title, recipientUserId, notificationType, title, message, traceId);
    private static IReadOnlyList<ProjectManagementTaskParticipantBatchReplaceItem> NormalizeBatchItems(IReadOnlyList<ProjectManagementTaskParticipantBatchReplaceItem> items)
    {
        var normalized = items.Select(item => new ProjectManagementTaskParticipantBatchReplaceItem(
            Required(item.TaskId, "任务不能为空"),
            (item.ParticipantUserIds ?? []).Where(userId => !string.IsNullOrWhiteSpace(userId)).Select(userId => userId.Trim()).Distinct(StringComparer.Ordinal).ToList())).ToList();
        if (normalized.Select(item => item.TaskId).Distinct(StringComparer.Ordinal).Count() != normalized.Count)
            throw new ValidationException("批量任务不能重复");
        if (normalized.Any(item => item.ParticipantUserIds.Count > 100))
            throw new ValidationException("单个任务参与人不能超过 100 人");
        return normalized;
    }
    private static string? FormatUserIds(IEnumerable<string> userIds)
    {
        var values = userIds.OrderBy(item => item, StringComparer.Ordinal).ToList();
        return values.Count == 0 ? null : string.Join(", ", values);
    }
    private static string ParticipantKey(string taskId, string userId) => $"{taskId}\u001f{userId}";
    private async Task WriteActivityAsync(ProjectManagementTaskEntity task, ProjectManagementTaskParticipantEntity participant, string activityType, string summary, bool assigned, string traceId, DateTime occurredAt, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            Tenant(), App(), "TaskParticipant", participant.Id, activityType, summary, traceId, User(), task.ProjectId,
            FieldChanges: [new ProjectManagementActivityFieldChange("UserId", "参与人", assigned ? null : participant.UserId, assigned ? participant.UserId : null)],
            OccurredAt: occurredAt), cancellationToken);
    }
    private async Task PublishNotificationAsync(ProjectManagementTaskEntity task, string recipientUserId, string notificationType, string title, string message, string traceId, CancellationToken cancellationToken)
    {
        if (notificationPublisher is null) return;
        await notificationPublisher.PublishAsync(new ProjectManagementNotification(Tenant(), App(), notificationType, recipientUserId, title, message, $"/projects/{task.ProjectId}/tasks?selectedTaskId={task.Id}", traceId, task.ProjectId, task.Id), cancellationToken);
    }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long request) { if (current != request || request <= 0) throw new ValidationException("任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
}
