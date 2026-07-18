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
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
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
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var owner = new ProjectManagementProjectMemberEntity { Id = "owner", TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", UserId = "operator", RoleCode = "Owner", CreatedBy = "operator", CreatedTime = DateTime.UtcNow };
        await db.Insertable(owner).ExecuteCommandAsync();
        var service = new ProjectManagementMemberService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), new AlwaysCandidateService());
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RemoveAsync("project-a", owner.Id, owner.VersionNo));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync("project-a", owner.Id, new ProjectManagementMemberUpsertRequest("operator", RoleCode: "Member", VersionNo: owner.VersionNo)));
    }

    [Fact]
    public async Task Milestones_validate_dates_and_soft_delete_with_version()
    {
        using var db = CreateDb("milestones");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementMilestoneService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementMilestoneUpsertRequest("invalid", StartDate: new DateTime(2026, 8, 2), DueDate: new DateTime(2026, 8, 1))));
        var created = await service.CreateAsync("project-a", new ProjectManagementMilestoneUpsertRequest("M1"));
        var updated = await service.UpdateAsync("project-a", created.Id, new ProjectManagementMilestoneUpsertRequest("M1 updated", VersionNo: created.VersionNo));
        await service.DeleteAsync("project-a", updated.Id, updated.VersionNo);
        Assert.Empty((await service.QueryAsync("project-a")).Items);
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

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "MES"),
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
}
