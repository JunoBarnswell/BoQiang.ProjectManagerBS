using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskTimeLogServiceTests
{
    [Fact]
    public async Task Time_logs_update_actual_minutes_atomically_and_validate_duration()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-time-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementTaskEntity { Id = "task-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "T-1", Title = "Task", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskTimeLogService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var invalidStart = DateTime.UtcNow;
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("task-a", new ProjectManagementTaskTimeLogUpsertRequest(invalidStart, invalidStart, VersionNo: 1)));
        var log = await service.CreateAsync("task-a", new ProjectManagementTaskTimeLogUpsertRequest(new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc), VersionNo: 1));
        Assert.Equal(60, log.Minutes);
        Assert.Equal(60, (await db.Queryable<ProjectManagementTaskEntity>().InSingleAsync("task-a")).ActualMinutes);
        await service.DeleteAsync("task-a", log.Id, log.VersionNo);
        Assert.Equal(0, (await db.Queryable<ProjectManagementTaskEntity>().InSingleAsync("task-a")).ActualMinutes);
    }

    [Fact]
    public void Time_log_controller_uses_task_view_and_edit_permissions()
    {
        Assert.Contains(typeof(ProjectManagementTaskTimeLogsController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskView);
        Assert.Contains(typeof(ProjectManagementTaskTimeLogsController).GetMethod(nameof(ProjectManagementTaskTimeLogsController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskEdit);
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM") }, "test")));
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
