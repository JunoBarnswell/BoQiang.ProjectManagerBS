using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementMemberCandidateTests
{
    [Fact]
    public void Project_management_module_registers_member_candidate_service()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        new AsterErpProjectManagementModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IProjectManagementMemberCandidateService));
    }

    [Fact]
    public void Member_candidate_endpoint_requires_project_view_permission()
    {
        var method = typeof(ProjectManagementMemberCandidatesController)
            .GetMethod(nameof(ProjectManagementMemberCandidatesController.QueryAsync));

        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes(typeof(PermissionAttribute), inherit: true),
            attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementProjectView);
    }

    [Fact]
    public async Task Candidate_query_is_paged_searchable_and_tenant_scoped()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-candidates-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        db.CodeFirst.InitTables<SystemUserEntity, SystemUserTenantMembershipEntity, SystemDepartmentEntity, SystemPositionEntity>();

        await db.Insertable(new SystemDepartmentEntity
        {
            Id = "dept-a",
            DeptCode = "A",
            DeptName = "项目部",
            Status = "Enabled"
        }).ExecuteCommandAsync();
        await db.Insertable(new SystemPositionEntity
        {
            Id = "position-a",
            PositionCode = "LEAD",
            PositionName = "项目负责人",
            DeptId = "dept-a",
            Status = "Enabled"
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserEntity { Id = "user-a", UserName = "alice", DisplayName = "Alice 项目负责人", Status = "Enabled" },
            new SystemUserEntity { Id = "user-b", UserName = "bob", DisplayName = "Bob 其他租户", Status = "Enabled" },
            new SystemUserEntity { Id = "user-c", UserName = "carol", DisplayName = "Carol 已停用", Status = "Disabled" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserTenantMembershipEntity
            {
                Id = "membership-a",
                UserId = "user-a",
                TenantId = "tenant-a",
                DeptId = "dept-a",
                PositionId = "position-a",
                IsDefault = true,
                Status = "Enabled"
            },
            new SystemUserTenantMembershipEntity
            {
                Id = "membership-b",
                UserId = "user-b",
                TenantId = "tenant-b",
                DeptId = "dept-a",
                PositionId = "position-a",
                IsDefault = true,
                Status = "Enabled"
            },
            new SystemUserTenantMembershipEntity
            {
                Id = "membership-c",
                UserId = "user-c",
                TenantId = "tenant-a",
                DeptId = "dept-a",
                PositionId = "position-a",
                IsDefault = true,
                Status = "Enabled"
            }
        }).ExecuteCommandAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, "operator"),
            new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
            new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
        }, "test"));
        var service = new ProjectManagementMemberCandidateService(
            new TestWorkspaceDatabaseAccessor(db),
            new FixedAsterErpCurrentUser(user));

        var result = await service.QueryAsync(
            new ProjectManagementMemberCandidateQuery(PageSize: 1, Keyword: "Alice"));

        Assert.Equal(1, result.Total);
        var candidate = Assert.Single(result.Items);
        Assert.Equal("user-a", candidate.UserId);
        Assert.Equal("membership-a", candidate.EmploymentId);
        Assert.Equal("项目部", candidate.DeptName);
        Assert.Equal("项目负责人", candidate.PositionName);
        Assert.True(candidate.IsSelectable);
    }

    [Fact]
    public async Task Candidate_selection_validates_the_exact_enabled_user_employment_pair()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-candidate-selection-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        db.CodeFirst.InitTables<SystemUserEntity, SystemUserTenantMembershipEntity>();
        await db.Insertable(new[]
        {
            new SystemUserEntity { Id = "user-a", UserName = "alice", DisplayName = "Alice", Status = "Enabled" },
            new SystemUserEntity { Id = "user-b", UserName = "bob", DisplayName = "Bob", Status = "Enabled" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new SystemUserTenantMembershipEntity { Id = "employment-a", UserId = "user-a", TenantId = "tenant-a", Status = "Enabled" },
            new SystemUserTenantMembershipEntity { Id = "employment-b", UserId = "user-b", TenantId = "tenant-a", Status = "Enabled" },
            new SystemUserTenantMembershipEntity { Id = "employment-disabled", UserId = "user-a", TenantId = "tenant-a", Status = "Disabled" },
            new SystemUserTenantMembershipEntity { Id = "employment-other-tenant", UserId = "user-a", TenantId = "tenant-b", Status = "Enabled" }
        }).ExecuteCommandAsync();

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, "operator"),
            new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
            new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
        }, "test"));
        var service = new ProjectManagementMemberCandidateService(
            new TestWorkspaceDatabaseAccessor(db),
            new FixedAsterErpCurrentUser(user));

        Assert.True(await service.IsSelectableAsync("user-a", "employment-a"));
        Assert.False(await service.IsSelectableAsync("user-a", "employment-b"));
        Assert.False(await service.IsSelectableAsync("user-a", "employment-disabled"));
        Assert.False(await service.IsSelectableAsync("user-a", "employment-other-tenant"));
        Assert.False(await service.IsSelectableAsync("user-a", "employment-missing"));
    }
}
