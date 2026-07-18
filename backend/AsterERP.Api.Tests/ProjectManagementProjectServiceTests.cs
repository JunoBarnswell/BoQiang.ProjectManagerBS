using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementProjectServiceTests
{
    [Fact]
    public void Project_controller_uses_menu_and_action_permissions()
    {
        var controller = typeof(ProjectManagementProjectsController);
        Assert.Contains(controller.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementProjectView);
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementProjectsController.CreateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementProjectAdd);
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementProjectsController.UpdateAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementProjectEdit);
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementProjectsController.DeleteAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementProjectDelete);
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementProjectsController.ArchiveAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementProjectArchive);
    }

    [Fact]
    public async Task Project_crud_preserves_workspace_boundary_soft_delete_and_optimistic_version()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-project-service-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "SYSTEM");
        var service = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), user);

        var created = await service.CreateAsync(new ProjectManagementProjectUpsertRequest(
            "PM-001", "项目一", "第一项目", "Planning", "High", null, new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), 5));
        Assert.Equal("tenant-a", created.TenantId);
        Assert.Equal("SYSTEM", created.AppCode);
        Assert.Equal(1, created.VersionNo);
        var owner = await db.Queryable<AsterERP.Api.Modules.ProjectManagement.ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == created.Id && !item.IsDeleted)
            .FirstAsync();
        Assert.Equal("operator", owner.UserId);
        Assert.Equal("Owner", owner.RoleCode);

        var page = await service.QueryAsync(new ProjectManagementProjectQuery(1, 20, "PM-001"));
        var listed = Assert.Single(page.Items);
        Assert.Equal(created.Id, listed.Id);

        var updated = await service.UpdateAsync(created.Id, new ProjectManagementProjectUpsertRequest(
            "PM-001", "项目一（更新）", VersionNo: created.VersionNo));
        Assert.Equal(2, updated.VersionNo);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() =>
            service.UpdateAsync(created.Id, new ProjectManagementProjectUpsertRequest("PM-001", "旧版本", VersionNo: 1)));

        await service.DeleteAsync(created.Id, updated.VersionNo);
        Assert.Empty((await service.QueryAsync(new ProjectManagementProjectQuery())).Items);

        var restored = await service.RestoreAsync(created.Id, 3);
        Assert.False(restored.VersionNo == updated.VersionNo);
        Assert.Single((await service.QueryAsync(new ProjectManagementProjectQuery())).Items);
    }

    [Fact]
    public async Task Project_create_rejects_invalid_dates_and_duplicate_codes()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-project-validation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementProjectService(
            new TestWorkspaceDatabaseAccessor(db),
            CreateUser("operator", "tenant-a", "SYSTEM"));

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync(
            new ProjectManagementProjectUpsertRequest("PM-INVALID", "无效项目", StartDate: new DateTime(2026, 8, 2), DueDate: new DateTime(2026, 8, 1))));
        await service.CreateAsync(new ProjectManagementProjectUpsertRequest("PM-DUP", "项目"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync(
            new ProjectManagementProjectUpsertRequest("PM-DUP", "重复项目")));
    }

    [Fact]
    public async Task Project_service_enforces_status_machine_and_dates_on_update()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-project-state-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementProjectService(
            new TestWorkspaceDatabaseAccessor(db),
            CreateUser("operator", "tenant-a", "SYSTEM"));

        var project = await service.CreateAsync(new ProjectManagementProjectUpsertRequest(
            "PM-STATE", "状态机项目", StartDate: new DateTime(2026, 7, 1), DueDate: new DateTime(2026, 7, 31)));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(
            project.Id,
            new ProjectManagementProjectUpsertRequest("PM-STATE", "状态机项目", Status: "Completed", VersionNo: project.VersionNo)));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(
            project.Id,
            new ProjectManagementProjectUpsertRequest("PM-STATE", "状态机项目", StartDate: new DateTime(2026, 8, 1), DueDate: new DateTime(2026, 7, 31), VersionNo: project.VersionNo)));

        var active = await service.UpdateAsync(
            project.Id,
            new ProjectManagementProjectUpsertRequest("PM-STATE", "状态机项目", Status: "Active", VersionNo: project.VersionNo));
        var paused = await service.UpdateAsync(
            project.Id,
            new ProjectManagementProjectUpsertRequest("PM-STATE", "状态机项目", Status: "Paused", VersionNo: active.VersionNo));
        var resumed = await service.UpdateAsync(
            project.Id,
            new ProjectManagementProjectUpsertRequest("PM-STATE", "状态机项目", Status: "Active", VersionNo: paused.VersionNo));
        var completed = await service.UpdateAsync(
            project.Id,
            new ProjectManagementProjectUpsertRequest("PM-STATE", "状态机项目", Status: "Completed", VersionNo: resumed.VersionNo));
        var archived = await service.UpdateAsync(
            project.Id,
            new ProjectManagementProjectUpsertRequest("PM-STATE", "状态机项目", Status: "Archived", VersionNo: completed.VersionNo));
        var canceledProject = await service.CreateAsync(new ProjectManagementProjectUpsertRequest("PM-CANCELED", "取消项目"));
        var canceled = await service.UpdateAsync(
            canceledProject.Id,
            new ProjectManagementProjectUpsertRequest("PM-CANCELED", "取消项目", Status: "Canceled", VersionNo: canceledProject.VersionNo));

        Assert.Equal("Archived", archived.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Equal("Canceled", canceled.Status);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync(
            new ProjectManagementProjectUpsertRequest("PM-INVALID-STATUS", "无效状态", Status: "Draft")));
    }

    [Fact]
    public async Task Project_query_does_not_leak_other_tenant_or_app_rows()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-project-isolation-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "VISIBLE", ProjectName = "Visible", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "other-tenant", TenantId = "tenant-b", AppCode = "SYSTEM", ProjectCode = "TENANT-B", ProjectName = "Tenant B", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "other-app", TenantId = "tenant-a", AppCode = "CRM", ProjectCode = "CRM", ProjectName = "CRM", OwnerUserId = "operator" }
        }).ExecuteCommandAsync();

        var user = CreateUser("operator", "tenant-a", "SYSTEM");
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(
            db,
            typeof(ProjectManagementProjectEntity),
            user,
            "tenant-a",
            ProjectManagementPlatformScope.AppCode));
        var service = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), user);
        var result = await service.QueryAsync(new ProjectManagementProjectQuery());

        var project = Assert.Single(result.Items);
        Assert.Equal("visible", project.Id);
    }

    [Fact]
    public async Task Project_service_requires_system_workspace_and_filters_by_status_and_owner()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-project-filters-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var systemUser = CreateUser("operator", "tenant-a", "SYSTEM");
        var service = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), systemUser);
        await service.CreateAsync(new ProjectManagementProjectUpsertRequest("PM-ACTIVE", "进行中", Status: "Active", OwnerUserId: "owner-a"));
        await service.CreateAsync(new ProjectManagementProjectUpsertRequest("PM-PAUSED", "已暂停", Status: "Paused", OwnerUserId: "owner-b"));

        var filtered = await service.QueryAsync(new ProjectManagementProjectQuery(Status: "Active", OwnerUserId: "owner-a"));
        Assert.Single(filtered.Items);
        Assert.Equal("PM-ACTIVE", filtered.Items[0].ProjectCode);

        var nonSystemService = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", "tenant-a", "MES"));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => nonSystemService.QueryAsync(new ProjectManagementProjectQuery()));
    }

    [Fact]
    public async Task Archived_project_is_read_only_and_requires_the_current_version()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-project-archive-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", "tenant-a", "SYSTEM"));
        var project = await service.CreateAsync(new ProjectManagementProjectUpsertRequest("PM-ARCHIVE", "归档项目"));

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.ArchiveAsync(project.Id, new ProjectManagementProjectArchiveRequest(project.VersionNo + 1)));
        var archived = await service.ArchiveAsync(project.Id, new ProjectManagementProjectArchiveRequest(project.VersionNo));

        Assert.Equal("Archived", archived.Status);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(archived.Id, new ProjectManagementProjectUpsertRequest("PM-ARCHIVE", "不应更新", Status: "Archived", VersionNo: archived.VersionNo)));
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId, string tenantId, string appCode)
    {
        return new FixedAsterErpCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, userId),
            new Claim(AsterErpClaimTypes.TenantId, tenantId),
            new Claim(AsterErpClaimTypes.AppCode, appCode),
            new Claim(AsterErpClaimTypes.DataScope, "SELF"),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementProjectView)
        }, "test")));
    }

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
