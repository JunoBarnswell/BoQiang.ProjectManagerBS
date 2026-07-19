using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementGanttScheduleServiceTests
{
    [Fact]
    public async Task Batch_schedule_updates_all_items_atomically_after_dependency_validation()
    {
        using var db = await CreateDbAsync();
        await InsertTasksAsync(db);
        var service = CreateService(db);

        var result = await service.UpdateAsync(new("project-a", [
            new("a", Day(2), Day(3), 1), new("b", Day(3), Day(4), 1)]));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(Day(2), await DateAsync(db, "a", true));
        Assert.Equal(Day(4), await DateAsync(db, "b", false));
        Assert.Equal(2, (await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "a").SingleAsync()).VersionNo);
    }

    [Fact]
    public async Task Dependency_or_version_failure_does_not_partially_write_batch()
    {
        using var db = await CreateDbAsync();
        await InsertTasksAsync(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(new("project-a", [
            new("a", Day(3), Day(4), 1), new("b", Day(2), Day(3), 1)])));
        Assert.Equal(Day(1), await DateAsync(db, "a", true));
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(new("project-a", [new("a", Day(2), Day(3), 99)])));
        Assert.Equal(Day(2), await DateAsync(db, "a", false));
    }

    [Fact]
    public async Task Project_boundary_and_object_permission_are_enforced()
    {
        using var db = await CreateDbAsync(projectDue: Day(4));
        await InsertTasksAsync(db);
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(db).UpdateAsync(new("project-a", [new("a", Day(2), Day(5), 1)])));
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(db, "outsider").UpdateAsync(new("project-a", [new("a", Day(2), Day(3), 1)])));
    }

    private static DateTime Day(int day) => new(2026, 7, day, 0, 0, 0, DateTimeKind.Utc);
    private static async Task<SqlSugarClient> CreateDbAsync(DateTime? projectDue = null)
    {
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:gantt-schedule-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator", StartDate = Day(1), DueDate = projectDue, CreatedTime = Day(1) }).ExecuteCommandAsync();
        return db;
    }
    private static async Task InsertTasksAsync(ISqlSugarClient db)
    {
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "A", Title = "A", StartDate = Day(1), DueDate = Day(2), CreatedBy = "operator", CreatedTime = Day(1) },
            new ProjectManagementTaskEntity { Id = "b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "B", Title = "B", StartDate = Day(2), DueDate = Day(3), CreatedBy = "operator", CreatedTime = Day(1) },
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskDependencyEntity { Id = "a-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", PredecessorTaskId = "a", SuccessorTaskId = "b", DependencyType = "FinishToStart", CreatedTime = Day(1) }).ExecuteCommandAsync();
    }
    private static async Task<DateTime?> DateAsync(ISqlSugarClient db, string id, bool start) { var task = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id).SingleAsync(); return start ? task.StartDate : task.DueDate; }
    private static ProjectManagementGanttScheduleService CreateService(ISqlSugarClient db, string userId = "operator") { var user = CreateUser(userId); var accessor = new TestWorkspaceDatabaseAccessor(db); return new(accessor, user, new ProjectManagementAccessPolicy(accessor, user)); }
    private static FixedAsterErpCurrentUser CreateUser(string id) => new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(AsterErpClaimTypes.UserId, id), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"), new Claim(AsterErpClaimTypes.DataScope, "SELF")], "test")));
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor { public ISqlSugarClient MainDb => db; public ISqlSugarClient GetCurrentDb() => db; public ISqlSugarClient RequireApplicationDb() => db; public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db); public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db); }
}
