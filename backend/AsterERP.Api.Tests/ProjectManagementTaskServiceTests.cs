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

public sealed class ProjectManagementTaskServiceTests
{
    [Fact]
    public async Task Tasks_support_hierarchy_move_cycle_detection_and_optimistic_concurrency()
    {
        using var db = CreateDb("tasks");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A",
            ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        var root = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-1", "Root"));
        var child = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-2", "Child", ParentTaskId: root.Id));
        Assert.Equal(0, root.Depth);
        Assert.Equal(1, child.Depth);

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() =>
            service.MoveAsync(root.Id, new ProjectManagementTaskMoveRequest(child.Id, 0, root.VersionNo)));
        var moved = await service.MoveAsync(child.Id, new ProjectManagementTaskMoveRequest(null, 2, child.VersionNo));
        Assert.Equal(0, moved.Depth);
        await db.Insertable(new ProjectManagementTaskDependencyEntity
        {
            ProjectId = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", PredecessorTaskId = root.Id,
            SuccessorTaskId = child.Id, CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        var board = await service.QueryAsync(new ProjectManagementTaskQuery("project-a", ViewKey: "board", SortBy: "status"));
        var blocked = Assert.Single(board.Items, item => item.Id == child.Id);
        Assert.Equal(1, blocked.BlockedByCount);
        Assert.False(blocked.CanStart);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() =>
            service.UpdateAsync(child.Id, new ProjectManagementTaskUpsertRequest("T-2", "Changed", VersionNo: child.VersionNo)));
    }

    [Fact]
    public async Task Tasks_reject_invalid_boundaries_and_delete_parent_with_its_subtree()
    {
        using var db = CreateDb("task-boundaries");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A",
            ProjectName = "A", OwnerUserId = "operator", WipLimit = 1
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        var first = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-1", "First", Status: "InProgress"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() =>
            service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-2", "Second", Status: "InProgress")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() =>
            service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-3", "Invalid", DueDate: DateTime.UtcNow.AddDays(-1), StartDate: DateTime.UtcNow)));

        var child = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-4", "Child", ParentTaskId: first.Id));
        await service.DeleteAsync(first.Id, first.VersionNo);
        var deletedRoot = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == first.Id).FirstAsync();
        var deletedChild = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == child.Id).FirstAsync();
        Assert.True(deletedRoot.IsDeleted);
        Assert.True(deletedChild.IsDeleted);
        Assert.Equal("operator", deletedRoot.DeletedBy);
        Assert.Equal(deletedRoot.DeletedTime, deletedChild.DeletedTime);
        var restored = await service.RestoreAsync(first.Id, deletedRoot.VersionNo);
        Assert.False((await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == first.Id).FirstAsync()).IsDeleted);
        Assert.Equal(first.Id, restored.Id);
    }

    [Fact]
    public async Task Tasks_expose_lightweight_page_and_validated_detail_crud()
    {
        using var db = CreateDb("task-list-detail");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity
        {
            Id = "member-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "member-a", RoleCode = "Member", IsActive = true, CreatedBy = "operator", CreatedTime = DateTime.UtcNow, JoinedAt = DateTime.UtcNow
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        var created = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-1", "任务", "完整详情", AssigneeUserId: "member-a", EstimateMinutes: 120, Weight: 2));
        var page = await service.QueryAsync(new ProjectManagementTaskQuery("project-a", PageIndex: 1, PageSize: 20));
        var listItem = Assert.Single(page.Items);
        Assert.Equal(created.Id, listItem.Id);
        Assert.Null(typeof(ProjectManagementTaskListItemResponse).GetProperty("Description"));

        var detail = await service.GetAsync(created.Id);
        Assert.Equal("完整详情", detail.Description);
        Assert.Equal(120, detail.EstimateMinutes);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() =>
            service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-2", "无效负责人", AssigneeUserId: "outside-user")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() =>
            service.QueryAsync(new ProjectManagementTaskQuery("project-a", DueFrom: DateTime.UtcNow.AddDays(1), DueTo: DateTime.UtcNow)));
    }

    [Fact]
    public async Task Task_estimate_is_the_default_progress_weight_and_explicit_weight_wins_in_parent_and_project_projection()
    {
        using var db = CreateDb("task-estimate-progress-weight");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        var root = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("ROOT", "Root"));
        var estimateDefault = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("ESTIMATE", "Estimated", ParentTaskId: root.Id, EstimateMinutes: 100, ProgressPercent: 10));
        var explicitWeight = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("EXPLICIT", "Explicit", ParentTaskId: root.Id, EstimateMinutes: 100, Weight: 1, ProgressPercent: 90));

        Assert.Equal(100m, estimateDefault.Weight);
        Assert.Equal(1m, explicitWeight.Weight);
        Assert.Equal(10.79m, (await service.GetAsync(root.Id)).ProgressPercent);
        Assert.Equal(10.79m, (await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == "project-a").FirstAsync()).ProgressPercent);
    }

    [Fact]
    public async Task Task_state_machine_normalizes_progress_dates_parent_completion_and_overdue_projection()
    {
        using var db = CreateDb("task-state-machine");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var parent = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("P", "Parent", ProgressPercent: 100));
        Assert.Equal(ProjectManagementDomainRules.TaskDone, parent.Status);
        Assert.NotNull(parent.ActualStartAt);
        Assert.NotNull(parent.ActualEndAt);
        var reopened = await service.UpdateAsync(parent.Id, new ProjectManagementTaskUpsertRequest("P", "Parent", Status: "Done", ProgressPercent: 20, VersionNo: parent.VersionNo));
        Assert.Equal(ProjectManagementDomainRules.TaskInProgress, reopened.Status);
        var child = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("C", "Child", ParentTaskId: parent.Id, DueDate: DateTime.UtcNow.AddDays(-1)));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(parent.Id, new ProjectManagementTaskUpsertRequest("P", "Parent", Status: "Done", ProgressPercent: 100, VersionNo: reopened.VersionNo)));
        var detail = await service.GetAsync(child.Id);
        Assert.True(detail.IsOverdue);
    }

    [Fact]
    public void Task_controller_separates_view_add_edit_move_and_delete_permissions()
    {
        Assert.Contains(typeof(ProjectManagementTasksController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskView);
        Assert.Contains(typeof(ProjectManagementTasksController).GetMethod(nameof(ProjectManagementTasksController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskAdd);
        Assert.Contains(typeof(ProjectManagementTasksController).GetMethod(nameof(ProjectManagementTasksController.UpdateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskEdit);
        Assert.Contains(typeof(ProjectManagementTasksController).GetMethod(nameof(ProjectManagementTasksController.MoveAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskMove);
        Assert.Contains(typeof(ProjectManagementTasksController).GetMethod(nameof(ProjectManagementTasksController.DeleteAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskDelete);
        Assert.Contains(typeof(ProjectManagementTasksController).GetMethod(nameof(ProjectManagementTasksController.RestoreAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementTaskRestore);
    }

    [Fact]
    public async Task Task_mutation_rolls_back_when_activity_write_fails()
    {
        using var db = CreateDb("task-transaction");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A",
            ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskService(
            new TestWorkspaceDatabaseAccessor(db),
            CreateUser(),
            activityWriter: new ThrowingActivityWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-1", "Will rollback")));
        Assert.False(await db.Queryable<ProjectManagementTaskEntity>().AnyAsync(item => item.ProjectId == "project-a"));
    }

    [Fact]
    public async Task Task_move_uses_sparse_sibling_order_and_rebalances_when_no_gap_remains()
    {
        using var db = CreateDb("task-sibling-order");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var first = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-1", "First"));
        var second = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-2", "Second"));
        var third = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-3", "Third"));

        Assert.Equal(new[] { 1024, 2048, 3072 }, (await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == "project-a")
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .Select(item => item.SortOrder)
            .ToListAsync()).ToArray());

        third = await service.MoveAsync(third.Id, new ProjectManagementTaskMoveRequest(null, 0, third.VersionNo, BeforeTaskId: first.Id));
        await db.Updateable<ProjectManagementTaskEntity>()
            .SetColumns(item => new ProjectManagementTaskEntity { SortOrder = 1025 })
            .Where(item => item.Id == second.Id)
            .ExecuteCommandAsync();
        await db.Updateable<ProjectManagementTaskEntity>()
            .SetColumns(item => new ProjectManagementTaskEntity { SortOrder = 2048 })
            .Where(item => item.Id == third.Id)
            .ExecuteCommandAsync();

        await service.MoveAsync(third.Id, new ProjectManagementTaskMoveRequest(null, 0, third.VersionNo, BeforeTaskId: second.Id));

        var ordered = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == "project-a")
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .ToListAsync();
        Assert.Equal(new[] { first.Id, third.Id, second.Id }, ordered.Select(item => item.Id).ToArray());
        Assert.Equal(new[] { 1024, 2048, 3072 }, ordered.Select(item => item.SortOrder).ToArray());
        Assert.Equal(3, ordered.Select(item => item.SortOrder).Distinct().Count());
    }

    [Fact]
    public async Task Task_move_updates_subtree_depth_milestone_and_progress_projection()
    {
        using var db = CreateDb("task-cross-parent-move");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementMilestoneEntity { Id = "milestone-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", MilestoneName = "A", CreatedBy = "operator", CreatedTime = DateTime.UtcNow },
            new ProjectManagementMilestoneEntity { Id = "milestone-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", MilestoneName = "B", CreatedBy = "operator", CreatedTime = DateTime.UtcNow }
        }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var root = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-1", "Root", MilestoneId: "milestone-a", ProgressPercent: 80));
        var child = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-2", "Child", MilestoneId: "milestone-a", ParentTaskId: root.Id, ProgressPercent: 50));
        var destination = await service.CreateAsync("project-a", new ProjectManagementTaskUpsertRequest("T-3", "Destination", MilestoneId: "milestone-b", ProgressPercent: 0));

        await service.MoveAsync(root.Id, new ProjectManagementTaskMoveRequest(destination.Id, 0, root.VersionNo, MilestoneId: "milestone-b", UpdateMilestone: true));

        var rows = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == "project-a").ToListAsync();
        var movedRoot = Assert.Single(rows, item => item.Id == root.Id);
        var movedChild = Assert.Single(rows, item => item.Id == child.Id);
        Assert.Equal(destination.Id, movedRoot.ParentTaskId);
        Assert.Equal(1, movedRoot.Depth);
        Assert.Equal(2, movedChild.Depth);
        Assert.Equal("milestone-b", movedRoot.MilestoneId);
        Assert.Equal("milestone-b", movedChild.MilestoneId);
        Assert.Equal(50, (await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == "project-a").FirstAsync()).ProgressPercent);
        Assert.Equal(50, (await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => item.Id == "milestone-b").FirstAsync()).ProgressPercent);
    }

    private static SqlSugarClient CreateDb(string name) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-{name}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser(string userId = "operator") => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId),
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

    private sealed class ThrowingActivityWriter : IProjectManagementActivityWriter
    {
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("activity write failed");
    }
}
