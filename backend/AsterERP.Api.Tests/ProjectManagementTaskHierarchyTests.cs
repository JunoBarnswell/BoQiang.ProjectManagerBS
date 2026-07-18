using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskHierarchyTests
{
    [Fact]
    public async Task Move_carries_full_subtree_rejects_cycles_and_preserves_depths()
    {
        using var db = CreateDb("hierarchy-move");
        await SeedProjectAsync(db, "project-a");
        var service = CreateService(db);

        var root = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("ROOT", "Root"));
        var child = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("CHILD", "Child", ParentTaskId: root.Id));
        var leaf = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("LEAF", "Leaf", ParentTaskId: child.Id));
        var destination = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("DEST", "Destination"));

        var moved = await service.MoveAsync(root.Id, new ProjectManagementTaskMoveRequest(destination.Id, 0, root.VersionNo));
        var rows = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == "project-a").ToListAsync();
        Assert.Equal(destination.Id, Assert.Single(rows, item => item.Id == root.Id).ParentTaskId);
        Assert.Equal(1, moved.Depth);
        Assert.Equal(2, Assert.Single(rows, item => item.Id == child.Id).Depth);
        Assert.Equal(3, Assert.Single(rows, item => item.Id == leaf.Id).Depth);

        var cycle = await Assert.ThrowsAsync<ValidationException>(() =>
            service.MoveAsync(destination.Id, new ProjectManagementTaskMoveRequest(leaf.Id, 0, destination.VersionNo)));
        Assert.Contains("->", cycle.Message, StringComparison.Ordinal);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.MoveAsync(root.Id, new ProjectManagementTaskMoveRequest(null, 0, root.VersionNo)));
    }

    [Fact]
    public async Task Hierarchy_rejects_cross_project_and_sixth_level()
    {
        using var db = CreateDb("hierarchy-limits");
        await SeedProjectAsync(db, "project-a");
        await SeedProjectAsync(db, "project-b");
        var service = CreateService(db);

        var current = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T0", "L0"));
        for (var level = 1; level < 5; level++)
            current = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest($"T{level}", $"L{level}", ParentTaskId: current.Id));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T5", "L5", ParentTaskId: current.Id)));

        var otherProjectTask = await service.CreateAsync("project-b", new ProjectManagementTaskUpsertRequest("OTHER", "Other"));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("CROSS", "Cross", ParentTaskId: otherProjectTask.Id)));
    }

    [Fact]
    public async Task Delete_can_promote_children_without_orphans()
    {
        using var db = CreateDb("hierarchy-promote");
        await SeedProjectAsync(db, "project-a");
        var service = CreateService(db);

        var root = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("ROOT", "Root"));
        var parent = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("PARENT", "Parent", ParentTaskId: root.Id));
        var child = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("CHILD", "Child", ParentTaskId: parent.Id));
        var grandchild = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("GRAND", "Grandchild", ParentTaskId: child.Id));

        await service.DeleteAsync(parent.Id, new ProjectManagementTaskDeleteRequest(parent.VersionNo, ProjectManagementTaskDeleteModes.PromoteChildren));

        var rows = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == "project-a").ToListAsync();
        Assert.True(Assert.Single(rows, item => item.Id == parent.Id).IsDeleted);
        var promoted = Assert.Single(rows, item => item.Id == child.Id);
        Assert.Equal(root.Id, promoted.ParentTaskId);
        Assert.Equal(1, promoted.Depth);
        Assert.Equal(child.Id, Assert.Single(rows, item => item.Id == grandchild.Id).ParentTaskId);
        Assert.Equal(2, Assert.Single(rows, item => item.Id == grandchild.Id).Depth);
    }

    private static ProjectManagementTaskService CreateService(SqlSugarClient db) =>
        new(new TestWorkspaceDatabaseAccessor(db), CreateUser());

    private static async Task SeedProjectAsync(SqlSugarClient db, string id)
    {
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        if (!await db.Queryable<ProjectManagementProjectEntity>().AnyAsync(item => item.Id == id))
            await db.Insertable(new ProjectManagementProjectEntity
            {
                Id = id,
                TenantId = "tenant-a",
                AppCode = "SYSTEM",
                ProjectCode = id,
                ProjectName = id,
                OwnerUserId = "operator"
            }).ExecuteCommandAsync();
    }

    private static SqlSugarClient CreateDb(string name) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.DataScope, "SELF"),
        new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementProjectView)
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
