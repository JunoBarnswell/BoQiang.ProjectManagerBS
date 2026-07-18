using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskReminderService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementReminderScheduler reminderScheduler,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null) : IProjectManagementTaskReminderService
{
    private const int MaximumRecipients = 50;
    private const int MaximumAttempts = 3;

    public async Task<IReadOnlyList<ProjectManagementTaskReminderResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await Policy().EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        var userId = User();
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskReminderEntity>()
            .Where(item => item.TaskId == task.Id && !item.IsDeleted && (item.RecipientUserId == userId || item.CreatedBy == userId))
            .OrderBy(item => item.ReminderAtUtc, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ProjectManagementTaskReminderResponse>> CreateAsync(string taskId, ProjectManagementTaskReminderCreateRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        var dueAt = NormalizeReminderAt(request.ReminderAt);
        var timeZoneId = NormalizeTimeZone(request.TimeZoneId);
        var clientRequestId = Required(request.ClientRequestId, "客户端请求标识不能为空", 128);
        var recipients = await ResolveRecipientsAsync(task, request.RecipientScope, request.RecipientUserIds, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var existing = await db.Queryable<ProjectManagementTaskReminderEntity>()
            .Where(item => item.TaskId == task.Id && !item.IsDeleted && recipients.Select(value => BuildIdempotencyKey(task.Id, value, clientRequestId)).Contains(item.IdempotencyKey))
            .ToListAsync(cancellationToken);
        var existingKeys = existing.Select(item => item.IdempotencyKey).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var created = new List<ProjectManagementTaskReminderEntity>();
        foreach (var recipientUserId in recipients)
        {
            var key = BuildIdempotencyKey(task.Id, recipientUserId, clientRequestId);
            if (existingKeys.Contains(key)) continue;
            var entity = new ProjectManagementTaskReminderEntity
            {
                TenantId = Tenant(), AppCode = App(), ProjectId = task.ProjectId, TaskId = task.Id,
                RecipientUserId = recipientUserId, ReminderAtUtc = dueAt, TimeZoneId = timeZoneId,
                Note = NormalizeNote(request.Note), Status = "Pending", IdempotencyKey = key,
                MaxAttempts = MaximumAttempts, CreatedBy = User(), CreatedTime = now, VersionNo = 1
            };
            entity.HangfireJobId = await reminderScheduler.ScheduleAsync(ToJobArgs(entity), new DateTimeOffset(entity.ReminderAtUtc), cancellationToken);
            created.Add(entity);
        }
        if (created.Count > 0)
        {
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                await db.Insertable(created).ExecuteCommandAsync(cancellationToken);
                await WriteActivityAsync(
                    task,
                    created.Count == 1 ? "新增任务提醒" : $"新增任务提醒（{created.Count} 位接收人）",
                    "task.reminder.created",
                    CreateChanges(created),
                    now,
                    CreateBatch(created),
                    cancellationToken);
            });
            await PublishInvalidationAsync(task, "task.reminder.created", cancellationToken);
        }
        return existing.Concat(created).OrderBy(item => item.ReminderAtUtc).Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskReminderResponse> UpdateAsync(string taskId, string id, ProjectManagementTaskReminderUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        var entity = await GetReminderAsync(task, id, cancellationToken);
        EnsureCanChange(entity);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        if (!string.Equals(entity.Status, "Pending", StringComparison.Ordinal)) throw new ValidationException("只有待触发提醒可以修改");
        var before = ReminderActivitySnapshot.From(entity);
        var nextVersion = entity.VersionNo + 1;
        var nextAt = NormalizeReminderAt(request.ReminderAt);
        var nextTimeZone = NormalizeTimeZone(request.TimeZoneId);
        var nextJobId = await reminderScheduler.ScheduleAsync(ToJobArgs(entity, nextVersion), new DateTimeOffset(nextAt), cancellationToken);
        var previousJobId = entity.HangfireJobId;
        entity.ReminderAtUtc = nextAt;
        entity.TimeZoneId = nextTimeZone;
        entity.Note = NormalizeNote(request.Note);
        entity.HangfireJobId = nextJobId;
        entity.AttemptCount = 0;
        entity.LastAttemptAt = null;
        entity.LastError = null;
        entity.VersionNo = nextVersion;
        entity.UpdatedBy = User();
        entity.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(task, "修改任务提醒时间", "task.reminder.updated", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, null, cancellationToken);
        });
        await reminderScheduler.DeleteAsync(previousJobId, cancellationToken);
        await PublishInvalidationAsync(task, "task.reminder.updated", cancellationToken);
        return Map(entity);
    }

    public async Task CancelAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        var entity = await GetReminderAsync(task, id, cancellationToken);
        EnsureCanChange(entity);
        EnsureVersion(entity.VersionNo, versionNo);
        if (!string.Equals(entity.Status, "Pending", StringComparison.Ordinal)) throw new ValidationException("只有待触发提醒可以取消");
        var before = ReminderActivitySnapshot.From(entity);
        entity.Status = "Canceled";
        entity.VersionNo++;
        entity.UpdatedBy = User();
        entity.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(task, "取消任务提醒", "task.reminder.canceled", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, null, cancellationToken);
        });
        await reminderScheduler.DeleteAsync(entity.HangfireJobId, cancellationToken);
        await PublishInvalidationAsync(task, "task.reminder.canceled", cancellationToken);
    }

    public async Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        var entity = await GetReminderAsync(task, id, cancellationToken);
        EnsureCanChange(entity);
        EnsureVersion(entity.VersionNo, versionNo);
        if (string.Equals(entity.Status, "Pending", StringComparison.Ordinal)) throw new ValidationException("待触发提醒请先取消");
        var before = ReminderActivitySnapshot.From(entity);
        entity.IsDeleted = true;
        entity.DeletedBy = User();
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = User();
        entity.UpdatedTime = entity.DeletedTime;
        entity.VersionNo++;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(task, "删除任务提醒记录", "task.reminder.deleted", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, null, cancellationToken);
        });
        await reminderScheduler.DeleteAsync(entity.HangfireJobId, cancellationToken);
        await PublishInvalidationAsync(task, "task.reminder.deleted", cancellationToken);
    }

    private async Task<ProjectManagementTaskEntity> GetTaskAsync(string taskId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == taskId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task<ProjectManagementTaskReminderEntity> GetReminderAsync(ProjectManagementTaskEntity task, string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskReminderEntity>().Where(item => item.Id == id && item.TaskId == task.Id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("任务提醒不存在", ErrorCodes.PlatformResourceNotFound);

    private async Task<IReadOnlyList<string>> ResolveRecipientsAsync(ProjectManagementTaskEntity task, string? recipientScope, IReadOnlyList<string>? requestedUsers, CancellationToken cancellationToken)
    {
        var scope = Required(recipientScope, "提醒对象不能为空", 32);
        IReadOnlyList<string> recipients = scope.ToLowerInvariant() switch
        {
            "self" => [User()],
            "assignee" when !string.IsNullOrWhiteSpace(task.AssigneeUserId) => [task.AssigneeUserId],
            "participants" => await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskParticipantEntity>().Where(item => item.TaskId == task.Id && !item.IsDeleted).Select(item => item.UserId).ToListAsync(cancellationToken),
            "members" => (requestedUsers ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaximumRecipients + 1).ToList(),
            "assignee" => throw new ValidationException("当前任务没有负责人，不能创建负责人提醒"),
            _ => throw new ValidationException("提醒对象只支持 Self、Assignee、Participants 或 Members")
        };
        recipients = recipients.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (recipients.Count == 0) throw new ValidationException("未找到有效提醒接收人");
        if (recipients.Count > MaximumRecipients) throw new ValidationException($"单次最多设置 {MaximumRecipients} 位提醒接收人");
        var activeMemberIds = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == task.ProjectId && item.IsActive && !item.IsDeleted && recipients.Contains(item.UserId))
            .Select(item => item.UserId).ToListAsync(cancellationToken);
        var owner = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == task.ProjectId && !item.IsDeleted).Select(item => item.OwnerUserId).ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(owner.FirstOrDefault()) && recipients.Contains(owner[0], StringComparer.OrdinalIgnoreCase)) activeMemberIds.Add(owner[0]);
        if (activeMemberIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != recipients.Count) throw new ValidationException("提醒接收人必须是当前项目的有效成员");
        return recipients;
    }

    private void EnsureCanChange(ProjectManagementTaskReminderEntity entity)
    {
        if (string.Equals(entity.RecipientUserId, User(), StringComparison.OrdinalIgnoreCase) || string.Equals(entity.CreatedBy, User(), StringComparison.OrdinalIgnoreCase)) return;
        throw new ValidationException("只能修改自己创建或接收的提醒", ErrorCodes.PermissionDenied);
    }

    private async Task WriteActivityAsync(
        ProjectManagementTaskEntity task,
        string summary,
        string activityType,
        IReadOnlyList<ProjectManagementActivityFieldChange> changes,
        DateTime occurredAt,
        ProjectManagementActivityBatch? batch,
        CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            Tenant(), App(), "Task", task.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), User(), task.ProjectId,
            Source: "User", FieldChanges: changes, Batch: batch, OccurredAt: occurredAt), cancellationToken);
    }

    private async Task PublishInvalidationAsync(ProjectManagementTaskEntity task, string eventType, CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "Task", task.Id, eventType, task.VersionNo, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), task.ProjectId), cancellationToken);
    }

    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private ProjectManagementReminderJobArgs ToJobArgs(ProjectManagementTaskReminderEntity entity, long? versionNo = null) => new(entity.Id, Tenant(), App(), entity.RecipientUserId, versionNo ?? entity.VersionNo);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static DateTime NormalizeReminderAt(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        if (utc < DateTime.UtcNow.AddMinutes(-1)) throw new ValidationException("提醒时间不能早于当前时间");
        return utc;
    }
    private static string NormalizeTimeZone(string? value)
    {
        var timeZoneId = Required(value, "时区不能为空", 128);
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); return timeZoneId; }
        catch (TimeZoneNotFoundException) { throw new ValidationException("时区不存在"); }
        catch (InvalidTimeZoneException) { throw new ValidationException("时区无效"); }
    }
    private static string? NormalizeNote(string? value) => string.IsNullOrWhiteSpace(value) ? null : Required(value, "提醒备注不能超过 1000 个字符", 1000);
    private static string Required(string? value, string error, int maximumLength) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength ? value.Trim() : throw new ValidationException(error);
    private static string BuildIdempotencyKey(string taskId, string recipientUserId, string clientRequestId) => $"reminder:{taskId}:{recipientUserId}:{clientRequestId}";
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("提醒已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static IReadOnlyList<ProjectManagementActivityFieldChange> CreateChanges(IReadOnlyList<ProjectManagementTaskReminderEntity> reminders) =>
        ProjectManagementActivityChanges.Collect(
            ProjectManagementActivityChanges.Create("RecipientUserIds", "接收人", null, string.Join(", ", reminders.Select(item => item.RecipientUserId))),
            ProjectManagementActivityChanges.Create("ReminderAtUtc", "提醒时间", null, reminders.Count == 1 ? reminders[0].ReminderAtUtc : null),
            ProjectManagementActivityChanges.Create("TimeZoneId", "时区", null, reminders.Count == 1 ? reminders[0].TimeZoneId : null),
            ProjectManagementActivityChanges.Create("Note", "提醒备注", null, reminders.Count == 1 ? reminders[0].Note : null, isSensitive: true),
            ProjectManagementActivityChanges.Create("Status", "状态", null, "Pending"));
    private static IReadOnlyList<ProjectManagementActivityFieldChange> CreateChanges(ReminderActivitySnapshot before, ProjectManagementTaskReminderEntity after) =>
        ProjectManagementActivityChanges.Collect(
            ProjectManagementActivityChanges.Create("ReminderAtUtc", "提醒时间", before.ReminderAtUtc, after.ReminderAtUtc),
            ProjectManagementActivityChanges.Create("TimeZoneId", "时区", before.TimeZoneId, after.TimeZoneId),
            ProjectManagementActivityChanges.Create("Note", "提醒备注", before.Note, after.Note, isSensitive: true),
            ProjectManagementActivityChanges.Create("Status", "状态", before.Status, after.Status),
            ProjectManagementActivityChanges.Create("IsDeleted", "已删除", before.IsDeleted, after.IsDeleted));
    private static ProjectManagementActivityBatch? CreateBatch(IReadOnlyList<ProjectManagementTaskReminderEntity> reminders)
    {
        if (reminders.Count <= 1) return null;
        var operationId = $"reminder:{reminders[0].TaskId}:{reminders[0].CreatedTime:O}";
        return new ProjectManagementActivityBatch(operationId, reminders.Count, reminders.Count, 0,
            reminders.Select(item => new ProjectManagementActivityBatchItem("TaskReminder", item.Id, $"创建提醒：{item.RecipientUserId}",
                ProjectManagementActivityChanges.Collect(
                    ProjectManagementActivityChanges.Create("ReminderAtUtc", "提醒时间", null, item.ReminderAtUtc),
                    ProjectManagementActivityChanges.Create("TimeZoneId", "时区", null, item.TimeZoneId),
                    ProjectManagementActivityChanges.Create("Note", "提醒备注", null, item.Note, isSensitive: true)))).ToList());
    }
    private sealed record ReminderActivitySnapshot(DateTime ReminderAtUtc, string TimeZoneId, string? Note, string Status, bool IsDeleted)
    {
        public static ReminderActivitySnapshot From(ProjectManagementTaskReminderEntity entity) => new(entity.ReminderAtUtc, entity.TimeZoneId, entity.Note, entity.Status, entity.IsDeleted);
    }
    private static ProjectManagementTaskReminderResponse Map(ProjectManagementTaskReminderEntity entity) => new(entity.Id, entity.ProjectId, entity.TaskId, entity.RecipientUserId, entity.ReminderAtUtc, entity.TimeZoneId, entity.Note, entity.Status, entity.AttemptCount, entity.MaxAttempts, entity.LastAttemptAt, entity.TriggeredAt, entity.LastError, entity.VersionNo, entity.CreatedTime);
}
