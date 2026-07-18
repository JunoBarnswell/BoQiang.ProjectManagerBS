using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementAccessPolicyTests
{
    [Fact]
    public async Task Viewer_cannot_manage_tasks_and_lead_can_manage_dependencies()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-policy-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "owner" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", UserId = "viewer", RoleCode = "Viewer" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "MES", ProjectId = "project-a", UserId = "lead", RoleCode = "Lead" }).ExecuteCommandAsync();

        var viewerPolicy = new ProjectManagementAccessPolicy(new TestWorkspaceDatabaseAccessor(db), CreateUser("viewer"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => viewerPolicy.EnsureCanManageTaskAsync("project-a"));
        var leadPolicy = new ProjectManagementAccessPolicy(new TestWorkspaceDatabaseAccessor(db), CreateUser("lead"));
        await leadPolicy.EnsureCanManageDependenciesAsync("project-a");
    }

    [Fact]
    public async Task Project_from_another_tenant_or_app_is_not_authorized()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-policy-isolation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-other-tenant", TenantId = "tenant-b", AppCode = "MES", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "project-other-app", TenantId = "tenant-a", AppCode = "CRM", ProjectCode = "C", ProjectName = "C", OwnerUserId = "operator" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectMemberEntity { TenantId = "tenant-b", AppCode = "MES", ProjectId = "project-other-tenant", UserId = "operator", RoleCode = "Owner" },
            new ProjectManagementProjectMemberEntity { TenantId = "tenant-a", AppCode = "CRM", ProjectId = "project-other-app", UserId = "operator", RoleCode = "Owner" }
        }).ExecuteCommandAsync();

        var policy = new ProjectManagementAccessPolicy(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator"));

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => policy.EnsureCanManageProjectAsync("project-other-tenant"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => policy.EnsureCanManageProjectAsync("project-other-app"));
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "MES")
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
