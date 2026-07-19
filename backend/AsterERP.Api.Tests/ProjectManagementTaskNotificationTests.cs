using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskNotificationTests
{
    [Fact]
    public async Task Task_assignee_and_due_date_changes_notify_the_current_assignee_and_active_participants()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-task-notification-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectMemberEntity { Id = "member-assignee", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "assignee", RoleCode = "Member", IsActive = true, CreatedBy = "operator", CreatedTime = DateTime.UtcNow, JoinedAt = DateTime.UtcNow },
            new ProjectManagementProjectMemberEntity { Id = "member-participant", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "participant", RoleCode = "Member", IsActive = true, CreatedBy = "operator", CreatedTime = DateTime.UtcNow, JoinedAt = DateTime.UtcNow }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "A-1", Title = "Task", Status = "Todo", Priority = "Medium", VersionNo = 1, CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskParticipantEntity { Id = "participant-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-a", UserId = "participant", RoleCode = "Participant", VersionNo = 1, CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        var notifications = new CapturingNotificationPublisher();
        var service = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), notificationPublisher: notifications);

        await service.UpdateAsync("task-a", new ProjectManagementTaskUpsertRequest("A-1", "Task", Status: "Todo", Priority: "Medium", AssigneeUserId: "assignee", DueDate: DateTime.UtcNow.Date.AddDays(3), VersionNo: 1));

        Assert.Contains(notifications.Notifications, item => item.NotificationType == ProjectManagementNotificationTypes.TaskAssigned && item.RecipientUserId == "assignee");
        Assert.Equal(2, notifications.Notifications.Count(item => item.NotificationType == ProjectManagementNotificationTypes.TaskDueDateChanged));
        Assert.DoesNotContain(notifications.Notifications, item => item.RecipientUserId == "operator");
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
    }, "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class CapturingNotificationPublisher : IProjectManagementNotificationPublisher
    {
        public List<ProjectManagementNotification> Notifications { get; } = [];
        public Task PublishAsync(ProjectManagementNotification notification, CancellationToken cancellationToken = default) { Notifications.Add(notification); return Task.CompletedTask; }
    }
}
