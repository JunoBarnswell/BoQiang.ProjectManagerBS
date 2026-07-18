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

public sealed class ProjectManagementTaskProgressProjectorTests
{
    [Fact]
    public async Task Refresh_uses_leaf_estimates_updates_parents_and_excludes_deleted_or_cancelled_tasks()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await SeedProjectAsync(db);
        var projector = new ProjectManagementTaskProgressProjector(new TestWorkspaceDatabaseAccessor(db));

        await projector.RefreshAsync("project-a");
        await projector.RefreshAsync("project-a");

        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(task => task.ProjectId == "project-a").ToListAsync();
        Assert.Equal(55m, Assert.Single(tasks, task => task.Id == "root").ProgressPercent);
        Assert.Equal(55m, Assert.Single(tasks, task => task.Id == "middle").ProgressPercent);
        Assert.Equal(0m, Assert.Single(tasks, task => task.Id == "empty-parent").ProgressPercent);
        Assert.Equal(54.75m, (await db.Queryable<ProjectManagementProjectEntity>().Where(project => project.Id == "project-a").FirstAsync()).ProgressPercent);

        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>().Where(milestone => milestone.ProjectId == "project-a").ToListAsync();
        Assert.Equal(55m, Assert.Single(milestones, milestone => milestone.Id == "milestone-a").ProgressPercent);
        Assert.Equal(0m, Assert.Single(milestones, milestone => milestone.Id == "milestone-b").ProgressPercent);
    }

    [Fact]
    public async Task Overview_counts_only_effective_leaves_with_done_blocked_and_overdue_semantics()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await SeedProjectAsync(db);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        await new ProjectManagementTaskProgressProjector(accessor).RefreshAsync("project-a");
        var user = CreateUser();
        var service = new ProjectManagementOverviewService(accessor, user, new ProjectManagementAccessPolicy(accessor, user));

        var item = Assert.Single((await service.QueryAsync(new ProjectManagementOverviewQuery())).Items);

        Assert.Equal(3, item.TaskCount);
        Assert.Equal(1, item.CompletedTaskCount);
        Assert.Equal(1, item.InProgressTaskCount);
        Assert.Equal(1, item.BlockedTaskCount);
        Assert.Equal(1, item.OverdueTaskCount);
        Assert.Equal(54.75m, item.TaskProgressPercent);
        var alice = Assert.Single(item.People, person => person.UserId == "alice");
        Assert.Equal(1, alice.CompletedTaskCount);
        Assert.Equal(0, alice.OverdueTaskCount);
        var bob = Assert.Single(item.People, person => person.UserId == "bob");
        Assert.Equal(0, bob.CompletedTaskCount);
        Assert.Equal(1, bob.OverdueTaskCount);
    }

    private static async Task SeedProjectAsync(ISqlSugarClient db)
    {
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementMilestoneEntity { Id = "milestone-a", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", MilestoneName = "A" },
            new ProjectManagementMilestoneEntity { Id = "milestone-b", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", MilestoneName = "B", ProgressPercent = 88m }
        }).ExecuteCommandAsync();
        var overdue = DateTime.UtcNow.AddDays(-1);
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "root", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "ROOT", Title = "Root", Status = ProjectManagementDomainRules.TaskInProgress, ProgressPercent = 99m },
            new ProjectManagementTaskEntity { Id = "middle", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", ParentTaskId = "root", TaskCode = "MIDDLE", Title = "Middle", Status = ProjectManagementDomainRules.TaskInProgress, ProgressPercent = 88m },
            new ProjectManagementTaskEntity { Id = "leaf-done", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", ParentTaskId = "middle", MilestoneId = "milestone-a", TaskCode = "DONE", Title = "Done", Status = ProjectManagementDomainRules.TaskDone, ProgressPercent = 100m, EstimateMinutes = 30, AssigneeUserId = "alice", DueDate = overdue },
            new ProjectManagementTaskEntity { Id = "leaf-blocked", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", ParentTaskId = "middle", MilestoneId = "milestone-a", TaskCode = "BLOCKED", Title = "Blocked", Status = ProjectManagementDomainRules.TaskBlocked, ProgressPercent = 40m, EstimateMinutes = 90, AssigneeUserId = "bob", DueDate = overdue },
            new ProjectManagementTaskEntity { Id = "leaf-unestimated", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "UNESTIMATED", Title = "Unestimated", Status = ProjectManagementDomainRules.TaskInProgress, ProgressPercent = 25m },
            new ProjectManagementTaskEntity { Id = "empty-parent", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "EMPTY", Title = "Empty parent", Status = ProjectManagementDomainRules.TaskInProgress, ProgressPercent = 77m },
            new ProjectManagementTaskEntity { Id = "cancelled-child", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", ParentTaskId = "empty-parent", TaskCode = "CANCELLED", Title = "Cancelled", Status = ProjectManagementDomainRules.TaskCancelled, ProgressPercent = 100m, EstimateMinutes = 600 },
            new ProjectManagementTaskEntity { Id = "deleted-leaf", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", TaskCode = "DELETED", Title = "Deleted", Status = ProjectManagementDomainRules.TaskDone, ProgressPercent = 100m, EstimateMinutes = 10_000, IsDeleted = true }
        }).ExecuteCommandAsync();
    }

    private static SqlSugarClient CreateDatabase() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-progress-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "MES"),
        new Claim(AsterErpClaimTypes.DataScope, "ALL")
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
