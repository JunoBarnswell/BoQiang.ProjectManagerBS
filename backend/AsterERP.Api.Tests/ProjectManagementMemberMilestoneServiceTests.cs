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

public sealed class ProjectManagementMemberMilestoneServiceTests
{
    [Fact]
    public async Task Members_are_scoped_to_project_and_support_versioned_remove()
    {
        using var db = CreateDb("members");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementMemberService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), new AlwaysCandidateService());

        var added = await service.AddAsync("project-a", new ProjectManagementMemberUpsertRequest("user-a", RoleCode: "Lead"));
        Assert.Equal("Lead", added.RoleCode);
        Assert.Single((await service.QueryAsync("project-a")).Items);
        var updated = await service.UpdateAsync("project-a", added.Id, new ProjectManagementMemberUpsertRequest("user-a", RoleCode: "Member", VersionNo: added.VersionNo));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RemoveAsync("project-a", added.Id, added.VersionNo));
        await service.RemoveAsync("project-a", added.Id, updated.VersionNo);
        Assert.Empty((await service.QueryAsync("project-a")).Items);
    }

    [Fact]
    public async Task Member_service_cannot_remove_or_demote_the_last_owner()
    {
        using var db = CreateDb("owner-guard");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var owner = new ProjectManagementProjectMemberEntity { Id = "owner", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "operator", RoleCode = "Owner", CreatedBy = "operator", CreatedTime = DateTime.UtcNow };
        await db.Insertable(owner).ExecuteCommandAsync();
        var service = new ProjectManagementMemberService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), new AlwaysCandidateService());
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RemoveAsync("project-a", owner.Id, owner.VersionNo));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync("project-a", owner.Id, new ProjectManagementMemberUpsertRequest("operator", RoleCode: "Member", VersionNo: owner.VersionNo)));
    }

    [Fact]
    public async Task Member_service_validates_lead_root_scope_and_writes_traceable_activity()
    {
        using var db = CreateDb("member-scope");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "root-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "A", Title = "A" },
            new ProjectManagementTaskEntity { Id = "child-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", ParentTaskId = "root-a", TaskCode = "A-1", Title = "A-1" },
            new ProjectManagementTaskEntity { Id = "root-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", TaskCode = "B", Title = "B" }
        }).ExecuteCommandAsync();
        var activityWriter = new RecordingActivityWriter();
        var service = new ProjectManagementMemberService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), new AlwaysCandidateService(), activityWriter: activityWriter);

        var lead = await service.AddAsync("project-a", new ProjectManagementMemberUpsertRequest("lead", RoleCode: "Lead", ScopeRootTaskId: "root-a"));
        Assert.Equal("root-a", lead.ScopeRootTaskId);
        var activity = Assert.Single(activityWriter.Events);
        Assert.Equal("ProjectMember", activity.AggregateType);
        Assert.Equal("added", activity.ActivityType);
        Assert.False(string.IsNullOrWhiteSpace(activity.TraceId));
        Assert.Contains(activity.FieldChanges!, change => change.Field == "ScopeRootTaskId" && change.After == "root-a");
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.AddAsync("project-a", new ProjectManagementMemberUpsertRequest("member", RoleCode: "Member", ScopeRootTaskId: "root-a")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.AddAsync("project-a", new ProjectManagementMemberUpsertRequest("lead-2", RoleCode: "Lead", ScopeRootTaskId: "child-a")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.AddAsync("project-a", new ProjectManagementMemberUpsertRequest("lead-3", RoleCode: "Lead", ScopeRootTaskId: "root-b")));
    }

    [Fact]
    public async Task Milestones_validate_dates_and_soft_delete_with_version()
    {
        using var db = CreateDb("milestones");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementMilestoneService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementMilestoneUpsertRequest("invalid", StartDate: new DateTime(2026, 8, 2), DueDate: new DateTime(2026, 8, 1))));
        var created = await service.CreateAsync("project-a", new ProjectManagementMilestoneUpsertRequest("M1"));
        var updated = await service.UpdateAsync("project-a", created.Id, new ProjectManagementMilestoneUpsertRequest("M1 updated", VersionNo: created.VersionNo));
        await service.DeleteAsync("project-a", updated.Id, updated.VersionNo);
        Assert.Empty((await service.QueryAsync("project-a")).Items);
    }

    [Fact]
    public async Task Milestones_return_leaf_task_summary_without_changing_task_dates()
    {
        using var db = CreateDb("milestone-summary");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementMilestoneService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var milestone = await service.CreateAsync("project-a", new ProjectManagementMilestoneUpsertRequest("M1", DueDate: DateTime.UtcNow.Date.AddDays(3)));
        var taskDueDate = DateTime.UtcNow.Date.AddDays(30);
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "parent", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", MilestoneId = milestone.Id, TaskCode = "PARENT", Title = "Parent", Status = "InProgress", ProgressPercent = 10, Weight = 1, CreatedTime = DateTime.UtcNow },
            new ProjectManagementTaskEntity { Id = "leaf", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", MilestoneId = milestone.Id, ParentTaskId = "parent", TaskCode = "LEAF", Title = "Leaf", Status = "Done", ProgressPercent = 100, Weight = 2, DueDate = taskDueDate, CreatedTime = DateTime.UtcNow }
        }).ExecuteCommandAsync();

        var summary = Assert.Single((await service.QueryAsync("project-a")).Items);
        Assert.Equal(1, summary.LeafTaskCount);
        Assert.Equal(1, summary.CompletedLeafTaskCount);
        Assert.Equal(100, summary.ProgressPercent);
        Assert.Equal("Done", summary.HealthStatus);

        await service.UpdateAsync("project-a", milestone.Id, new ProjectManagementMilestoneUpsertRequest("M1", DueDate: DateTime.UtcNow.Date.AddDays(5), VersionNo: milestone.VersionNo));
        var task = Assert.Single(await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == "leaf").ToListAsync());
        Assert.Equal(taskDueDate, task.DueDate);
    }

    [Fact]
    public async Task Milestone_writes_require_project_owner_or_manager()
    {
        using var db = CreateDb("milestone-access");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "owner" }).ExecuteCommandAsync();
        var service = new ProjectManagementMilestoneService(new TestWorkspaceDatabaseAccessor(db), CreateUser("viewer"));

        var exception = await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementMilestoneUpsertRequest("M1")));
        Assert.Equal(ErrorCodes.PermissionDenied, exception.Code);
    }

    [Fact]
    public void Member_and_milestone_controllers_require_view_and_manage_permissions()
    {
        Assert.Contains(typeof(ProjectManagementMembersController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementMemberView);
        Assert.Contains(typeof(ProjectManagementMilestonesController).GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementMilestoneView);
        Assert.Contains(typeof(ProjectManagementMembersController).GetMethod(nameof(ProjectManagementMembersController.AddAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementMemberManage);
        Assert.Contains(typeof(ProjectManagementMilestonesController).GetMethod(nameof(ProjectManagementMilestonesController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementMilestoneManage);
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

    private sealed class AlwaysCandidateService : IProjectManagementMemberCandidateService
    {
        public Task<bool> IsSelectableAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<GridPageResult<ProjectManagementMemberCandidateResponse>> QueryAsync(ProjectManagementMemberCandidateQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingActivityWriter : IProjectManagementActivityWriter
    {
        public List<ProjectManagementActivityEvent> Events { get; } = [];
        public Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default)
        {
            Events.Add(activity);
            return Task.CompletedTask;
        }
    }
}
