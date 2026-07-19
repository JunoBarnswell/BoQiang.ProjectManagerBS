using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
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
        var notification = new ProjectManagementNotification("tenant-a", "SYSTEM", "task.comment.mentioned", "user-a", "Mention", "Message", "/projects/p/tasks/t/comments/c", "trace");
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
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "user-a" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task" }).ExecuteCommandAsync();
        var service = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a"));
        await service.PublishAsync(new ProjectManagementNotification("tenant-a", "SYSTEM", "task.reminder", "user-a", "Reminder", "Message", "/untrusted", "trace-a", "project-a", "task-a"));
        await service.PublishAsync(new ProjectManagementNotification("tenant-a", "SYSTEM", "task.reminder", "user-a", "Stale", "Message", "/untrusted", "trace-b", "missing", "missing"));
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

    [Fact]
    public async Task Notification_persistence_survives_realtime_transport_failure()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-notification-fallback-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a"), realtimeTransport: new ThrowingRealtimeTransport());

        await service.PublishAsync(new ProjectManagementNotification("tenant-a", "SYSTEM", "task.reminder", "user-a", "Reminder", "Message", "/projects/project-a/tasks", "transport-failure"));

        var page = await service.QueryAsync(new ProjectManagementNotificationQuery());
        Assert.Single(page.Items);
        Assert.Equal("transport-failure", page.Items[0].TraceId);
    }

    [Fact]
    public async Task Notification_actions_cannot_cross_tenant_or_application_scope()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-notification-scope-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementNotificationEntity
        {
            Id = "foreign-notification", TenantId = "tenant-b", AppCode = "SYSTEM", RecipientUserId = "user-a",
            NotificationType = "task.reminder", Title = "Foreign", Message = "Foreign", TargetRoute = "/projects/foreign/tasks",
            TraceId = "foreign", IdempotencyKey = "foreign", CreatedBy = "user-a", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        var service = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a"));

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.NotFoundException>(() => service.MarkReadAsync("foreign-notification"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.NotFoundException>(() => service.OpenAsync("foreign-notification"));
    }

    [Fact]
    public async Task Notification_idempotency_is_isolated_by_tenant_and_application()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-notification-idempotency-scope-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var tenantA = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a", "tenant-a", "SYSTEM"));
        var tenantB = new ProjectManagementNotificationService(new TestWorkspaceDatabaseAccessor(db), CreateUser("user-a", "tenant-b", "MES"));
        var notificationA = new ProjectManagementNotification("tenant-a", "SYSTEM", ProjectManagementNotificationTypes.TaskAssigned, "user-a", "Assignment", "Message", "/projects/project-a/tasks", "same-business-event", "project-a");
        var notificationB = notificationA with { TenantId = "tenant-b", AppCode = "MES" };

        await tenantA.PublishAsync(notificationA);
        await tenantB.PublishAsync(notificationB);

        Assert.Single((await tenantA.QueryAsync(new ProjectManagementNotificationQuery())).Items);
        Assert.Single((await tenantB.QueryAsync(new ProjectManagementNotificationQuery())).Items);
        Assert.Equal(2, await db.Queryable<ProjectManagementNotificationEntity>().CountAsync());
    }

    [Fact]
    public async Task Terminal_operation_writes_a_safe_notification_that_opens_the_authorized_audit_center()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-notification-operation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var currentUser = CreateUser("user-a", permissions: [PermissionCodes.ProjectManagementOperationView]);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var notifications = new ProjectManagementNotificationService(accessor, currentUser);
        await db.Insertable(new ProjectManagementOperationEntity
        {
            Id = "operation-a", TenantId = "tenant-a", AppCode = "SYSTEM", OperationType = "sync.import", Status = "Running", Phase = "Importing",
            TraceId = "trace-a", ActorUserId = "user-a", StartedTime = DateTime.UtcNow, CreatedBy = "user-a", CreatedTime = DateTime.UtcNow, VersionNo = 1
        }).ExecuteCommandAsync();
        var writer = new ProjectManagementOperationWriter(accessor, currentUser, notificationPublisher: notifications);

        await writer.SucceedAsync("operation-a");

        var notification = Assert.Single((await notifications.QueryAsync(new ProjectManagementNotificationQuery())).Items);
        Assert.Equal(ProjectManagementNotificationTypes.OperationSucceeded, notification.NotificationType);
        Assert.Equal("/project-audit-center", (await notifications.OpenAsync(notification.Id)).TargetRoute);
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId, string tenantId = "tenant-a", string appCode = "SYSTEM", params string[] permissions) => new(new ClaimsPrincipal(new ClaimsIdentity(
        new[]
        {
            new Claim(AsterErpClaimTypes.UserId, userId), new Claim(AsterErpClaimTypes.TenantId, tenantId), new Claim(AsterErpClaimTypes.AppCode, appCode)
        }.Concat(permissions.Select(permission => new Claim(AsterErpClaimTypes.PermissionCode, permission))), "test")));
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class ThrowingRealtimeTransport : IProjectManagementRealtimeTransport
    {
        public Task PublishNotificationCreatedAsync(string tenantId, string appCode, string recipientUserId, string notificationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("SignalR unavailable");
        public Task PublishOperationProgressAsync(string tenantId, string appCode, string userId, ProjectManagementOperationProgressEvent progressEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishInvalidationAsync(string tenantId, string appCode, string projectId, ProjectManagementRealtimeEvent invalidation, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeProjectAccessAsync(string tenantId, string appCode, string projectId, string connectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
