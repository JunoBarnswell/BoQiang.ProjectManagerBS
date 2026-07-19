using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskReminderServiceTests
{
    [Fact]
    public async Task Reminder_create_is_idempotent_and_update_cancel_reschedules_safely()
    {
        using var db = CreateDatabase("reminder-crud");
        await SeedTaskAsync(db, "operator", "user-a");
        var scheduler = new RecordingScheduler();
        var service = new ProjectManagementTaskReminderService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), scheduler);
        var request = new ProjectManagementTaskReminderCreateRequest(DateTimeOffset.UtcNow.AddMinutes(10), "UTC", "Members", ["user-a"], "review", "request-1");
        var first = Assert.Single(await service.CreateAsync("task-a", request));
        var duplicate = Assert.Single(await service.CreateAsync("task-a", request));
        Assert.Equal(first.Id, duplicate.Id);
        Assert.Single(scheduler.Scheduled);

        var updated = await service.UpdateAsync("task-a", first.Id, new ProjectManagementTaskReminderUpdateRequest(DateTimeOffset.UtcNow.AddMinutes(20), "UTC", "rescheduled", first.VersionNo));
        Assert.Equal(2, scheduler.Scheduled.Count);
        Assert.Contains(scheduler.Deleted, jobId => jobId == "job-1");
        await service.CancelAsync("task-a", updated.Id, updated.VersionNo);
        var entity = await db.Queryable<ProjectManagementTaskReminderEntity>().FirstAsync(item => item.Id == first.Id);
        Assert.Equal("Canceled", entity.Status);
        Assert.Contains(scheduler.Deleted, jobId => jobId == "job-2");
    }

    [Fact]
    public async Task Reminder_create_compensates_scheduled_jobs_when_persistence_fails()
    {
        using var db = CreateDatabase("reminder-compensation");
        await SeedTaskAsync(db, "operator", "user-a");
        var scheduler = new RecordingScheduler();
        var service = new ProjectManagementTaskReminderService(
            new TestWorkspaceDatabaseAccessor(db),
            CreateUser(),
            scheduler,
            activityWriter: new ThrowingActivityWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            "task-a",
            new ProjectManagementTaskReminderCreateRequest(DateTimeOffset.UtcNow.AddMinutes(10), "UTC", "Members", ["user-a"], "review", "request-compensation")));

        Assert.Equal(["job-1"], scheduler.Deleted);
        Assert.Empty(await db.Queryable<ProjectManagementTaskReminderEntity>().ToListAsync());
    }

    [Fact]
    public async Task Reminder_execution_is_idempotent_and_does_not_send_after_task_is_deleted()
    {
        using var db = CreateDatabase("reminder-execution");
        await SeedTaskAsync(db, "user-a", "user-a");
        var reminder = new ProjectManagementTaskReminderEntity
        {
            Id = "reminder-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", RecipientUserId = "user-a",
            ReminderAtUtc = DateTime.UtcNow.AddMinutes(-1), TimeZoneId = "UTC", IdempotencyKey = "key-a", CreatedBy = "user-a", MaxAttempts = 3
        };
        await db.Insertable(reminder).ExecuteCommandAsync();
        var publisher = new RecordingNotificationPublisher();
        var service = new ProjectManagementReminderExecutionService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a"), publisher);
        var args = new ProjectManagementReminderJobArgs(reminder.Id, "tenant-a", "SYSTEM", "user-a", reminder.VersionNo);
        await service.ExecuteAsync(args);
        await service.ExecuteAsync(args);
        Assert.Single(publisher.Notifications);
        var sent = await db.Queryable<ProjectManagementTaskReminderEntity>().FirstAsync(item => item.Id == reminder.Id);
        Assert.Equal("Sent", sent.Status);

        var canceled = new ProjectManagementTaskReminderEntity
        {
            Id = "reminder-deleted-task", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", RecipientUserId = "user-a",
            ReminderAtUtc = DateTime.UtcNow.AddMinutes(-1), TimeZoneId = "UTC", IdempotencyKey = "key-deleted", CreatedBy = "user-a", MaxAttempts = 3
        };
        var deletedTask = await db.Queryable<ProjectManagementTaskEntity>().FirstAsync(item => item.Id == "task-a");
        deletedTask.IsDeleted = true;
        await db.Updateable(deletedTask).ExecuteCommandAsync();
        await db.Insertable(canceled).ExecuteCommandAsync();
        await service.ExecuteAsync(new ProjectManagementReminderJobArgs(canceled.Id, "tenant-a", "SYSTEM", "user-a", 1));
        Assert.Single(publisher.Notifications);
        Assert.Equal("Canceled", (await db.Queryable<ProjectManagementTaskReminderEntity>().FirstAsync(item => item.Id == canceled.Id)).Status);
    }

    [Fact]
    public async Task Task_delete_cancels_pending_reminders_and_removes_their_jobs()
    {
        using var db = CreateDatabase("reminder-task-delete");
        await SeedTaskAsync(db, "operator", "operator");
        await db.Insertable(new ProjectManagementTaskReminderEntity
        {
            Id = "reminder-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", RecipientUserId = "operator",
            ReminderAtUtc = DateTime.UtcNow.AddMinutes(10), TimeZoneId = "UTC", IdempotencyKey = "key-a", HangfireJobId = "job-a", CreatedBy = "operator"
        }).ExecuteCommandAsync();
        var scheduler = new RecordingScheduler();
        var taskService = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), reminderScheduler: scheduler);
        await taskService.DeleteAsync("task-a", 1);
        Assert.Equal("Canceled", (await db.Queryable<ProjectManagementTaskReminderEntity>().FirstAsync(item => item.Id == "reminder-a")).Status);
        Assert.Contains("job-a", scheduler.Deleted);
    }

    [Fact]
    public async Task Reminder_data_filter_hides_unrelated_project_rows()
    {
        using var db = CreateDatabase("reminder-filter");
        await SeedTaskAsync(db, "owner-a", "user-a");
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "owner-b" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", TaskCode = "T-2", Title = "Other" }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskReminderEntity { Id = "visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", RecipientUserId = "user-a", ReminderAtUtc = DateTime.UtcNow.AddMinutes(10), TimeZoneId = "UTC", IdempotencyKey = "visible" },
            new ProjectManagementTaskReminderEntity { Id = "hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", TaskId = "task-b", RecipientUserId = "user-b", ReminderAtUtc = DateTime.UtcNow.AddMinutes(10), TimeZoneId = "UTC", IdempotencyKey = "hidden" },
        }).ExecuteCommandAsync();
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskReminderEntity), CreateUser("user-a"), "tenant-a", "SYSTEM"));
        var rows = await db.Queryable<ProjectManagementTaskReminderEntity>().OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(["visible"], rows.Select(item => item.Id));
    }

    [Fact]
    public async Task Reminder_execution_records_terminal_failure_after_max_attempts()
    {
        using var db = CreateDatabase("reminder-failure");
        await SeedTaskAsync(db, "user-a", "user-a");
        var reminder = new ProjectManagementTaskReminderEntity
        {
            Id = "reminder-failure", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", RecipientUserId = "user-a",
            ReminderAtUtc = DateTime.UtcNow.AddMinutes(-1), TimeZoneId = "UTC", IdempotencyKey = "failure", CreatedBy = "user-a", MaxAttempts = 1
        };
        await db.Insertable(reminder).ExecuteCommandAsync();
        var service = new ProjectManagementReminderExecutionService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a"), new ThrowingNotificationPublisher());
        await service.ExecuteAsync(new ProjectManagementReminderJobArgs(reminder.Id, "tenant-a", "SYSTEM", "user-a", reminder.VersionNo));
        var failed = await db.Queryable<ProjectManagementTaskReminderEntity>().FirstAsync(item => item.Id == reminder.Id);
        Assert.Equal("Failed", failed.Status);
        Assert.Equal(1, failed.AttemptCount);
        Assert.NotNull(failed.LastError);
    }

    [Fact]
    public void Reminder_controller_separates_view_and_manage_permissions()
    {
        Assert.Contains(typeof(ProjectManagementTaskRemindersController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementReminderView);
        Assert.Contains(typeof(ProjectManagementTaskRemindersController).GetMethod(nameof(ProjectManagementTaskRemindersController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementReminderManage);
    }

    private static async Task SeedTaskAsync(ISqlSugarClient db, string ownerUserId, string memberUserId)
    {
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = ownerUserId }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = memberUserId, RoleCode = "Manager", IsActive = true }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = ownerUserId, CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
    }

    private static SqlSugarClient CreateDatabase(string name) => new(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
    private static FixedAsterErpCurrentUser CreateUser(string userId = "operator") => new(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AsterErpClaimTypes.UserId, userId), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM") }, "test")));

    private sealed class RecordingScheduler : IProjectManagementReminderScheduler
    {
        public List<ProjectManagementReminderJobArgs> Scheduled { get; } = [];
        public List<string> Deleted { get; } = [];
        public Task<string> ScheduleAsync(ProjectManagementReminderJobArgs args, DateTimeOffset scheduledAt, CancellationToken cancellationToken = default) { Scheduled.Add(args); return Task.FromResult($"job-{Scheduled.Count}"); }
        public Task DeleteAsync(string? jobId, CancellationToken cancellationToken = default) { if (!string.IsNullOrWhiteSpace(jobId)) Deleted.Add(jobId); return Task.CompletedTask; }
    }

    private sealed class RecordingNotificationPublisher : IProjectManagementNotificationPublisher
    {
        public List<ProjectManagementNotification> Notifications { get; } = [];
        public Task PublishAsync(ProjectManagementNotification notification, CancellationToken cancellationToken = default) { Notifications.Add(notification); return Task.CompletedTask; }
    }

    private sealed class ThrowingNotificationPublisher : IProjectManagementNotificationPublisher
    {
        public Task PublishAsync(ProjectManagementNotification notification, CancellationToken cancellationToken = default) => throw new InvalidOperationException("notification transport unavailable");
    }

    private sealed class ThrowingActivityWriter : IProjectManagementActivityWriter
    {
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default) => throw new InvalidOperationException("activity failure");
    }

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
