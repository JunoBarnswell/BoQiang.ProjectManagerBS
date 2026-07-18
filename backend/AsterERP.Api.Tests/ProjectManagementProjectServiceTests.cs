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
        Assert.Contains(controller.GetMethod(nameof(ProjectManagementProjectsController.RestoreAsync))!.GetCustomAttributes(typeof(PermissionAttribute), true), attribute => ((PermissionAttribute)attribute).Code == PermissionCodes.ProjectManagementProjectRestore);
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
        await Assert.ThrowsAsync<ProjectManagementProjectVersionConflictException>(() =>
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
    public async Task Project_query_uses_database_paging_and_combined_search_status_and_owner_filters()
    {
        using var db = CreateDb("query-projection");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "matched-new", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "PM-MATCH-NEW", ProjectName = "匹配项目", Description = "关键字", Status = "Active", OwnerUserId = "operator", CreatedTime = new DateTime(2026, 7, 2) },
            new ProjectManagementProjectEntity { Id = "matched-old", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "PM-MATCH-OLD", ProjectName = "匹配项目", Description = "关键字", Status = "Active", OwnerUserId = "operator", CreatedTime = new DateTime(2026, 7, 1) },
            new ProjectManagementProjectEntity { Id = "wrong-status", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "PM-MATCH-PAUSED", ProjectName = "匹配项目", Description = "关键字", Status = "Paused", OwnerUserId = "operator", CreatedTime = new DateTime(2026, 7, 3) },
            new ProjectManagementProjectEntity { Id = "wrong-keyword", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "PM-OTHER", ProjectName = "其他项目", Status = "Active", OwnerUserId = "operator", CreatedTime = new DateTime(2026, 7, 4) }
        }).ExecuteCommandAsync();
        var service = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", "tenant-a", "SYSTEM"));
        var query = new ProjectManagementProjectQuery(PageSize: 1, Keyword: "MATCH", Status: "Active", OwnerUserId: "operator");

        var firstPage = await service.QueryAsync(query);
        var secondPage = await service.QueryAsync(query with { PageIndex = 2 });

        Assert.Equal(2, firstPage.Total);
        Assert.Equal("matched-new", Assert.Single(firstPage.Items).Id);
        Assert.Equal(2, secondPage.Total);
        Assert.Equal("matched-old", Assert.Single(secondPage.Items).Id);
    }

    [Fact]
    public async Task Project_service_rejects_direct_update_by_non_owner_or_manager()
    {
        using var db = CreateDb("direct-authorization");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "managed-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "PM-MANAGED",
            ProjectName = "受控项目", Status = "Planning", OwnerUserId = "owner", VersionNo = 1
        }).ExecuteCommandAsync();
        var service = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), CreateUser("intruder", "tenant-a", "SYSTEM"));

        var exception = await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(
            "managed-project", new ProjectManagementProjectUpsertRequest("PM-MANAGED", "越权更新", VersionNo: 1)));

        Assert.Equal(ErrorCodes.PermissionDenied, exception.Code);
        var persisted = await db.Queryable<ProjectManagementProjectEntity>().SingleAsync(item => item.Id == "managed-project");
        Assert.Equal("受控项目", persisted.ProjectName);
        Assert.Equal(1, persisted.VersionNo);
    }

    [Fact]
    public async Task Archived_project_is_read_only_and_requires_the_current_version()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-project-archive-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var service = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), CreateUser("operator", "tenant-a", "SYSTEM"));
        var project = await service.CreateAsync(new ProjectManagementProjectUpsertRequest("PM-ARCHIVE", "归档项目"));

        await Assert.ThrowsAsync<ProjectManagementProjectVersionConflictException>(() => service.ArchiveAsync(project.Id, new ProjectManagementProjectArchiveRequest(project.VersionNo + 1)));
        var archived = await service.ArchiveAsync(project.Id, new ProjectManagementProjectArchiveRequest(project.VersionNo));

        Assert.Equal("Archived", archived.Status);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.UpdateAsync(archived.Id, new ProjectManagementProjectUpsertRequest("PM-ARCHIVE", "不应更新", Status: "Archived", VersionNo: archived.VersionNo)));
    }

    [Fact]
    public async Task Project_version_conflict_returns_server_local_and_conflicting_fields_with_http_409()
    {
        using var db = CreateDb("conflict");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "SYSTEM");
        var service = new ProjectManagementProjectService(new TestWorkspaceDatabaseAccessor(db), user);
        var created = await service.CreateAsync(new ProjectManagementProjectUpsertRequest("PM-CONFLICT", "初始名称"));
        await service.UpdateAsync(created.Id, new ProjectManagementProjectUpsertRequest("PM-CONFLICT", "服务端名称", VersionNo: created.VersionNo));
        var controller = new ProjectManagementProjectsController(service)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        var result = await controller.UpdateAsync(
            created.Id,
            new ProjectManagementProjectUpsertRequest("PM-CONFLICT", "本地名称", VersionNo: created.VersionNo),
            CancellationToken.None);

        var response = Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(result);
        Assert.Equal(Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict, response.StatusCode);
        var envelope = Assert.IsType<ApiResult<ProjectManagementProjectVersionConflictResponse>>(response.Value);
        Assert.Equal("服务端名称", envelope.Data!.ServerValues.ProjectName);
        Assert.Equal("本地名称", envelope.Data.LocalValues.ProjectName);
        var projectNameConflict = Assert.Single(envelope.Data.FieldConflicts, item => item.Field == "ProjectName");
        Assert.Equal("服务端名称", projectNameConflict.ServerValue);
        Assert.Equal("本地名称", projectNameConflict.LocalValue);
        Assert.Contains(envelope.Data.FieldConflicts, item => item.Field == "VersionNo");
    }

    [Fact]
    public async Task Project_delete_hides_normal_pages_and_restore_preserves_independent_child_deletions()
    {
        using var db = CreateDb("restore-visibility");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var user = CreateUser("operator", "tenant-a", "SYSTEM");
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var service = new ProjectManagementProjectService(accessor, user);
        var project = await service.CreateAsync(new ProjectManagementProjectUpsertRequest("PM-RESTORE", "恢复语义"));
        await db.Insertable(new[]
        {
            new ProjectManagementMilestoneEntity { Id = "milestone-active", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = project.Id, MilestoneName = "保留里程碑", Status = "Planned" },
            new ProjectManagementMilestoneEntity { Id = "milestone-deleted", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = project.Id, MilestoneName = "独立删除里程碑", Status = "Planned", IsDeleted = true }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-active", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = project.Id, TaskCode = "ACTIVE", Title = "保留任务", Status = "Todo" },
            new ProjectManagementTaskEntity { Id = "task-deleted", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = project.Id, TaskCode = "DELETED", Title = "独立删除任务", Status = "Todo", IsDeleted = true }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity
        {
            Id = "member-deleted", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = project.Id,
            UserId = "former-member", RoleCode = "Member", IsActive = false, IsDeleted = true
        }).ExecuteCommandAsync();
        var tasks = new ProjectManagementTaskService(accessor, user);
        var milestones = new ProjectManagementMilestoneService(accessor, user);

        Assert.Single((await service.QueryAsync(new ProjectManagementProjectQuery())).Items);
        Assert.Single((await tasks.QueryAsync(new ProjectManagementTaskQuery(project.Id))).Items);
        Assert.Single((await milestones.QueryAsync(project.Id)).Items);

        await service.DeleteAsync(project.Id, project.VersionNo);

        Assert.Empty((await service.QueryAsync(new ProjectManagementProjectQuery())).Items);
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.NotFoundException>(() => tasks.QueryAsync(new ProjectManagementTaskQuery(project.Id)));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.NotFoundException>(() => milestones.QueryAsync(project.Id));

        var restored = await service.RestoreAsync(project.Id, 2);

        Assert.Single((await service.QueryAsync(new ProjectManagementProjectQuery())).Items);
        Assert.Single((await tasks.QueryAsync(new ProjectManagementTaskQuery(project.Id))).Items);
        Assert.Single((await milestones.QueryAsync(project.Id)).Items);
        Assert.True((await db.Queryable<ProjectManagementProjectMemberEntity>().SingleAsync(item => item.Id == "member-deleted")).IsDeleted);
        Assert.True((await db.Queryable<ProjectManagementMilestoneEntity>().SingleAsync(item => item.Id == "milestone-deleted")).IsDeleted);
        Assert.True((await db.Queryable<ProjectManagementTaskEntity>().SingleAsync(item => item.Id == "task-deleted")).IsDeleted);

        var duplicateRestore = await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RestoreAsync(project.Id, restored.VersionNo));
        Assert.Equal("项目未删除，不能恢复", duplicateRestore.Message);
        Assert.Equal(restored.VersionNo, (await db.Queryable<ProjectManagementProjectEntity>().SingleAsync(item => item.Id == project.Id)).VersionNo);
    }

    private static SqlSugarClient CreateDb(string scenario) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-project-{scenario}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

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
