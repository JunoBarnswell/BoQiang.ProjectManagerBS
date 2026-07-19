using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementReminderExecutionService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementNotificationPublisher notificationPublisher,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null)
{
    public async Task ExecuteAsync(ProjectManagementReminderJobArgs args, CancellationToken cancellationToken = default)
    {
        EnsureContext(args);
        var db = databaseAccessor.GetCurrentDb();
        var reminder = (await db.Queryable<ProjectManagementTaskReminderEntity>()
            .Where(item => item.Id == args.ReminderId && item.RecipientUserId == args.RecipientUserId && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (reminder is null || reminder.VersionNo != args.VersionNo || !string.Equals(reminder.Status, "Pending", StringComparison.Ordinal)) return;
        var now = DateTime.UtcNow;
        if (reminder.ReminderAtUtc > now.AddSeconds(5)) return;
        if (reminder.AttemptCount >= reminder.MaxAttempts)
        {
            await MarkFailedAsync(db, reminder, "提醒已达到最大重试次数", cancellationToken);
            return;
        }
        var nextAttempt = reminder.AttemptCount + 1;
        var claimed = await db.Updateable<ProjectManagementTaskReminderEntity>()
            .SetColumns(item => new ProjectManagementTaskReminderEntity
            {
                AttemptCount = nextAttempt,
                LastAttemptAt = now,
                LastError = null,
                UpdatedBy = User(),
                UpdatedTime = now
            })
            .Where(item => item.Id == reminder.Id && item.VersionNo == args.VersionNo && item.AttemptCount == reminder.AttemptCount && item.Status == "Pending" && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);
        if (claimed != 1) return;
        reminder.AttemptCount = nextAttempt;
        reminder.LastAttemptAt = now;
        reminder.LastError = null;
        reminder.UpdatedBy = User();
        reminder.UpdatedTime = now;
        try
        {
            var task = (await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == reminder.TaskId && item.ProjectId == reminder.ProjectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
            if (task is null)
            {
                reminder.Status = "Canceled";
                reminder.UpdatedTime = DateTime.UtcNow;
                await db.Updateable(reminder).UpdateColumns(item => new { item.Status, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
                return;
            }
            var traceId = $"reminder:{reminder.Id}:{reminder.VersionNo}";
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                await notificationPublisher.PublishAsync(new ProjectManagementNotification(
                    Tenant(), App(), "task.reminder", reminder.RecipientUserId, "任务提醒",
                    BuildMessage(task, reminder), $"/projects/{task.ProjectId}/tasks?selectedTaskId={task.Id}", traceId, task.ProjectId, task.Id), cancellationToken);
                reminder.Status = "Sent";
                reminder.TriggeredAt = DateTime.UtcNow;
                reminder.LastError = null;
                reminder.UpdatedTime = reminder.TriggeredAt;
                await db.Updateable(reminder).UpdateColumns(item => new { item.Status, item.TriggeredAt, item.LastError, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
                if (activityWriter is not null)
                    await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), "TaskReminder", reminder.Id, "task.reminder.sent", "发送任务提醒", traceId, User(), task.ProjectId), cancellationToken);
            });
            if (realtimePublisher is not null)
                await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "TaskReminder", reminder.Id, "task.reminder.sent", reminder.VersionNo, traceId, reminder.ProjectId), cancellationToken);
        }
        catch (Exception ex)
        {
            if (reminder.AttemptCount >= reminder.MaxAttempts)
            {
                await MarkFailedAsync(db, reminder, ex.Message, cancellationToken);
                return;
            }
            reminder.LastError = TrimError(ex.Message);
            reminder.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(reminder).UpdateColumns(item => new { item.LastError, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
            throw;
        }
    }

    private async Task MarkFailedAsync(ISqlSugarClient db, ProjectManagementTaskReminderEntity reminder, string error, CancellationToken cancellationToken)
    {
        reminder.Status = "Failed";
        reminder.LastError = TrimError(error);
        reminder.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(reminder).UpdateColumns(item => new { item.Status, item.LastError, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    private void EnsureContext(ProjectManagementReminderJobArgs args)
    {
        if (!string.Equals(Tenant(), args.TenantId, StringComparison.Ordinal) || !string.Equals(App(), args.AppCode, StringComparison.OrdinalIgnoreCase) || !string.Equals(User(), args.RecipientUserId, StringComparison.Ordinal))
            throw new InvalidOperationException("提醒作业上下文不匹配");
    }

    private static string BuildMessage(ProjectManagementTaskEntity task, ProjectManagementTaskReminderEntity reminder) =>
        string.IsNullOrWhiteSpace(reminder.Note) ? $"任务「{task.Title}」提醒" : $"任务「{task.Title}」提醒：{reminder.Note}";
    private static string TrimError(string? value) => string.IsNullOrWhiteSpace(value) ? "提醒投递失败" : value.Length <= 1000 ? value : value[..1000];
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new InvalidOperationException("提醒作业缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new InvalidOperationException("提醒作业缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new InvalidOperationException("提醒作业缺少接收人");
}
