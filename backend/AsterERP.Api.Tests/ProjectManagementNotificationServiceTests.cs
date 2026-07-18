using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementNotificationServiceTests
{
    [Fact]
    public async Task Notification_publish_is_idempotent_and_read_state_is_recipient_scoped()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-notification-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a"));
        var notification = new ProjectManagementNotification("tenant-a", "MES", "task.comment.mentioned", "user-a", "Mention", "Message", "/projects/p/tasks/t/comments/c", "trace");
        await service.PublishAsync(notification);
        await service.PublishAsync(notification);
        await service.PublishAsync(notification with { TraceId = "trace-2", Message = "Message 2" });
        var items = (await service.QueryAsync(new ProjectManagementNotificationQuery(UnreadOnly: true))).Items;
        Assert.Equal(2, items.Count);
        await service.MarkReadAsync(items[0].Id);
        Assert.Single((await service.QueryAsync(new ProjectManagementNotificationQuery(UnreadOnly: true))).Items);

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.PublishAsync(notification with { TenantId = "tenant-b" }));
    }

    [Fact]
    public async Task Notification_supports_unread_summary_mark_all_and_safe_target_opening()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-notification-open-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "user-a" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "T-1", Title = "Task" }).ExecuteCommandAsync();
        var service = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a"));
        await service.PublishAsync(new ProjectManagementNotification("tenant-a", "MES", "task.reminder", "user-a", "Reminder", "Message", "/untrusted", "trace-a", "project-a", "task-a"));
        await service.PublishAsync(new ProjectManagementNotification("tenant-a", "MES", "task.reminder", "user-a", "Stale", "Message", "/untrusted", "trace-b", "missing", "missing"));
        var page = await service.QueryAsync(new ProjectManagementNotificationQuery(PageSize: 10));
        Assert.Equal(2, page.UnreadCount);
        await service.MarkAllReadAsync();
        Assert.Equal(0, (await service.QueryAsync(new ProjectManagementNotificationQuery(PageSize: 10))).UnreadCount);
        var opened = await service.OpenAsync(page.Items.Single(item => item.Title == "Reminder").Id);
        Assert.True(opened.IsAvailable);
        Assert.Equal("/projects/project-a/tasks?selectedTaskId=task-a", opened.TargetRoute);
        var stale = await service.OpenAsync(page.Items.Single(item => item.Title == "Stale").Id);
        Assert.False(stale.IsAvailable);
        Assert.Null(stale.TargetRoute);
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "MES")
    }, "test")));
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
