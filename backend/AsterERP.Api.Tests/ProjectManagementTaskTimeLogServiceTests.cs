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
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectManagementTaskTimeLogEntity { Minutes = -1 });
        var invalidStart = DateTime.UtcNow;
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("task-a", new ProjectManagementTaskTimeLogUpsertRequest(invalidStart, invalidStart, VersionNo: 1)));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("task-a", new ProjectManagementTaskTimeLogUpsertRequest(new DateTime(2026, 7, 18, 8, 0, 1, DateTimeKind.Utc), new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc), VersionNo: 1)));
        var log = await service.CreateAsync("task-a", new ProjectManagementTaskTimeLogUpsertRequest(new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc), VersionNo: 1));
        Assert.Equal(60, log.Minutes);
        Assert.Equal(60, (await db.Queryable<ProjectManagementTaskEntity>().InSingleAsync("task-a")).ActualMinutes);
        var updated = await service.UpdateAsync("task-a", log.Id, new ProjectManagementTaskTimeLogUpdateRequest(new DateTime(2026, 7, 18, 7, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc), "updated", log.VersionNo, 2));
        var taskAfterUpdate = await db.Queryable<ProjectManagementTaskEntity>().InSingleAsync("task-a");
        Assert.Equal(120, taskAfterUpdate.ActualMinutes);
        Assert.Equal(updated.StartedAt, taskAfterUpdate.ActualStartAt);
        Assert.Equal(updated.EndedAt, taskAfterUpdate.ActualEndAt);
        await service.DeleteAsync("task-a", updated.Id, updated.VersionNo);
        var taskAfterDelete = await db.Queryable<ProjectManagementTaskEntity>().InSingleAsync("task-a");
        Assert.Equal(0, taskAfterDelete.ActualMinutes);
        Assert.Null(taskAfterDelete.ActualStartAt);
        Assert.Null(taskAfterDelete.ActualEndAt);
    }

    [Fact]
    public async Task Workload_is_aggregated_by_assignee_and_keeps_retired_user_time_logs()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-workload-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", CreatedTime = DateTime.UtcNow }).ExecuteCommandAsync();
        var now = DateTime.UtcNow;
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "todo", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "TODO", Title = "Todo", Status = ProjectManagementDomainRules.TaskTodo, AssigneeUserId = "active-user", EstimateMinutes = 30, DueDate = now.AddDays(-1), CreatedTime = now },
            new ProjectManagementTaskEntity { Id = "progress", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "PROGRESS", Title = "Progress", Status = ProjectManagementDomainRules.TaskInProgress, AssigneeUserId = "active-user", EstimateMinutes = 45, DueDate = now.AddDays(1), CreatedTime = now },
            new ProjectManagementTaskEntity { Id = "done", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "DONE", Title = "Done", Status = ProjectManagementDomainRules.TaskDone, AssigneeUserId = "active-user", EstimateMinutes = 15, DueDate = now.AddDays(-2), CreatedTime = now }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskTimeLogEntity { Id = "active-log", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "todo", UserId = "active-user", StartedAt = new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc), EndedAt = new DateTime(2026, 7, 18, 8, 20, 0, DateTimeKind.Utc), Minutes = 20, CreatedTime = now },
            new ProjectManagementTaskTimeLogEntity { Id = "retired-log", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "todo", UserId = "retired-user", StartedAt = new DateTime(2026, 7, 18, 9, 0, 0, DateTimeKind.Utc), EndedAt = new DateTime(2026, 7, 18, 9, 15, 0, DateTimeKind.Utc), Minutes = 15, CreatedTime = now }
        }).ExecuteCommandAsync();

        var service = new ProjectManagementTaskTimeLogService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var rows = await service.QueryWorkloadAsync(new ProjectManagementTaskWorkloadQuery("project-a", new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 18, 23, 59, 0, DateTimeKind.Utc)));

        var active = Assert.Single(rows, item => item.UserId == "active-user");
        Assert.Equal(1, active.TodoTaskCount);
        Assert.Equal(1, active.InProgressTaskCount);
        Assert.Equal(1, active.CompletedTaskCount);
        Assert.Equal(1, active.OverdueTaskCount);
        Assert.Equal(90, active.EstimatedMinutes);
        Assert.Equal(20, active.LoggedMinutes);
        var retired = Assert.Single(rows, item => item.UserId == "retired-user");
        Assert.Equal(15, retired.LoggedMinutes);
        Assert.Equal(0, retired.EstimatedMinutes);
    }

    [Fact]
    public void Time_log_controller_uses_task_view_and_edit_permissions()
    {
        Assert.Contains(typeof(ProjectManagementTaskTimeLogsController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskView);
        Assert.Contains(typeof(ProjectManagementTaskTimeLogsController).GetMethod(nameof(ProjectManagementTaskTimeLogsController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskEdit);
        Assert.Contains(typeof(ProjectManagementTaskTimeLogsController).GetMethod(nameof(ProjectManagementTaskTimeLogsController.UpdateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskEdit);
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
