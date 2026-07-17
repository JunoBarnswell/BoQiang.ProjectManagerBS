using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Parameters;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDatabaseBaselineSeederTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"astererp-app-baseline-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task SeedAsync_WritesWorkflowMenusOnlyWhenWorkflowCapabilityIsEnabled()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = new SystemUserEntity
        {
            Id = "app-admin-user",
            DisplayName = "应用管理员",
            IsAdmin = true,
            PasswordHash = "hash",
            UserName = "app_admin"
        };

        await seeder.SeedAsync(
            db,
            "tenant-a",
            "MES",
            currentUser,
            CancellationToken.None,
            "{\"shellCapabilities\":[\"Workflow\"]}");

        var workflowMenus = await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && item.MenuCode.StartsWith("workflow") && !item.IsDeleted)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .ToListAsync();
        var menuCodes = workflowMenus.Select(item => item.MenuCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("workflow", menuCodes);
        Assert.Contains("workflow:workspace", menuCodes);
        Assert.Contains("workflow:management", menuCodes);
        Assert.Contains("workflow:analytics", menuCodes);
        Assert.Contains("workflow:settings", menuCodes);
        Assert.Contains("workflow:history", menuCodes);
        Assert.Contains("workflow:bindings", menuCodes);
        Assert.Equal("/workflows/bindings", workflowMenus.First(item => item.MenuCode == "workflow:bindings").RoutePath);
        Assert.All(workflowMenus, item => Assert.Equal("ApplicationShell", item.ScopeType));

        var permissionCodes = await db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled && item.ModuleName == "Workflow")
            .Select(item => item.PermissionCode)
            .ToListAsync();
        Assert.Contains(PermissionCodes.WorkflowModelPublish, permissionCodes);
        Assert.Contains(PermissionCodes.WorkflowBindingEdit, permissionCodes);
        Assert.Contains(PermissionCodes.WorkflowTaskApprove, permissionCodes);
        Assert.Contains(PermissionCodes.WorkflowHistoryQuery, permissionCodes);
        Assert.Contains(PermissionCodes.WorkflowNotificationRuleEdit, permissionCodes);

        var appAdminRole = await db.Queryable<SystemRoleEntity>()
            .FirstAsync(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && item.RoleCode == "app_admin" && !item.IsDeleted);
        var bindingEditPermission = await db.Queryable<SystemPermissionCodeEntity>()
            .FirstAsync(item => item.PermissionCode == PermissionCodes.WorkflowBindingEdit && !item.IsDeleted);
        var hasBindingGrant = await db.Queryable<SystemRolePermissionEntity>()
            .AnyAsync(item => item.RoleId == appAdminRole.Id && item.PermissionCodeId == bindingEditPermission.Id && !item.IsDeleted);
        Assert.True(hasBindingGrant);

        var workflowUsers = await db.Queryable<SystemUserEntity>()
            .Where(item => item.UserName.StartsWith("wf_") && !item.IsDeleted)
            .OrderBy(item => item.UserName)
            .ToListAsync();
        Assert.Contains(workflowUsers, item => item.UserName == "wf_starter" && item.Status == "Enabled");
        Assert.Contains(workflowUsers, item => item.UserName == "wf_delegate" && item.Status == "Enabled");
        Assert.Contains(workflowUsers, item => item.UserName == "wf_no_permission" && item.Status == "Enabled");

        var financeDepartment = await db.Queryable<SystemDepartmentEntity>()
            .FirstAsync(item => item.Id == "wf-finance" && !item.IsDeleted);
        Assert.Equal("[\"wf_manager_approver\",\"wf_position_approver\",\"wf_dept_approver\"]", financeDepartment.LeaderUserIdsJson);

        var starterRole = await db.Queryable<SystemRoleEntity>()
            .FirstAsync(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && item.RoleCode == "wf_starter" && !item.IsDeleted);
        var starterPermissions = await db.Queryable<SystemRolePermissionEntity>()
            .Where(item => item.RoleId == starterRole.Id && !item.IsDeleted)
            .InnerJoin<SystemPermissionCodeEntity>((grant, permission) => grant.PermissionCodeId == permission.Id && !permission.IsDeleted)
            .Select((grant, permission) => permission.PermissionCode)
            .ToListAsync();
        Assert.Contains(PermissionCodes.WorkflowDraftEdit, starterPermissions);
        Assert.Contains(PermissionCodes.WorkflowDelegationEdit, starterPermissions);
        Assert.Contains(PermissionCodes.BuildAppRuntimePagePermission("codex_microflow_order_demo", "view"), starterPermissions);

        var noPermissionRole = await db.Queryable<SystemRoleEntity>()
            .FirstAsync(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && item.RoleCode == "wf_no_permission" && !item.IsDeleted);
        var noPermissionWorkflowGrant = await db.Queryable<SystemRolePermissionEntity>()
            .Where(item => item.RoleId == noPermissionRole.Id && !item.IsDeleted)
            .InnerJoin<SystemPermissionCodeEntity>((grant, permission) =>
                grant.PermissionCodeId == permission.Id &&
                permission.ModuleName == "Workflow" &&
                !permission.IsDeleted)
            .AnyAsync();
        Assert.False(noPermissionWorkflowGrant);

        var baselineVersion = await db.Queryable<SystemParameterEntity>()
            .Where(item => item.ParamKey == ApplicationDatabaseBaselineSeeder.BaselineParameterKey && !item.IsDeleted)
            .Select(item => item.ParamValue)
            .FirstAsync();
        Assert.Equal(ApplicationDatabaseBaselineSeeder.BaselineVersion, baselineVersion);
    }

    [Fact]
    public async Task SeedAsync_DefaultShellDoesNotWriteOptionalCapabilityMenus()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = new SystemUserEntity
        {
            Id = "app-admin-user",
            DisplayName = "应用管理员",
            IsAdmin = true,
            PasswordHash = "hash",
            UserName = "app_admin"
        };

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None);

        var activeMenuCodes = await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && !item.IsDeleted)
            .Select(item => item.MenuCode)
            .ToListAsync();
        Assert.Contains("home", activeMenuCodes);
        Assert.Contains("app-console", activeMenuCodes);
        Assert.DoesNotContain("app-center", activeMenuCodes);
        Assert.DoesNotContain("workflow", activeMenuCodes);
        Assert.DoesNotContain("ai", activeMenuCodes);
        Assert.DoesNotContain("system", activeMenuCodes);
        Assert.DoesNotContain("asterscene", activeMenuCodes);
    }

    [Fact]
    public async Task SeedAsync_RegistersDevelopmentCenterMonitoringPermission()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        var currentUser = CreateCurrentUser();

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None);

        var permission = await db.Queryable<SystemPermissionCodeEntity>()
            .FirstAsync(item => item.PermissionCode == PermissionCodes.AppDevelopmentCenterMonitoringWrite && !item.IsDeleted && item.IsEnabled);
        var appAdminRole = await db.Queryable<SystemRoleEntity>()
            .FirstAsync(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && item.RoleCode == "app_admin" && !item.IsDeleted);

        var isGranted = await db.Queryable<SystemRolePermissionEntity>()
            .AnyAsync(item => item.RoleId == appAdminRole.Id && item.PermissionCodeId == permission.Id && !item.IsDeleted);

        Assert.True(isGranted);
    }

    [Fact]
    public async Task SeedAsync_PublishesRuntimeMenuModelInApplicationDatabase()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();

        await seeder.SeedAsync(db, "tenant-a", "WMS", CreateCurrentUser(), CancellationToken.None);

        var model = await db.Queryable<SystemDataModelEntity>()
            .FirstAsync(item =>
                item.TenantId == "tenant-a" &&
                item.AppCode == "WMS" &&
                item.ModelCode == ApplicationRuntimeDataModelCatalog.RuntimeMenuModelCode &&
                !item.IsDeleted);

        Assert.Equal("Published", model.Status);
        Assert.Equal(ApplicationRuntimeDataModelCatalog.RuntimeMenuProviderKey, model.ProviderKey);
        Assert.Contains("menuCode", model.SchemaJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedAsync_WithoutExplicitCapabilitiesDoesNotRestoreLegacyOptionalShellMenus()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = new SystemUserEntity
        {
            Id = "app-admin-user",
            DisplayName = "应用管理员",
            IsAdmin = true,
            PasswordHash = "hash",
            UserName = "app_admin"
        };
        await db.Insertable(new[]
        {
            CreateLegacyShellMenu("system", "系统设置", null, null, null, "Directory", 100),
            CreateLegacyShellMenu("system:user", "用户管理", "system", "/system/users", PermissionCodes.SystemUserQuery, "Menu", 101),
            CreateLegacyShellMenu("ai", "智能中心", null, null, null, "Directory", 700),
            CreateLegacyShellMenu("ai:workbench", "AI 工作台", "ai", "/ai/workbench", PermissionCodes.AiWorkbenchView, "Menu", 701)
        }).ExecuteCommandAsync();

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None);

        var activeMenuCodes = await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && !item.IsDeleted && item.Visible)
            .Select(item => item.MenuCode)
            .ToListAsync();

        Assert.DoesNotContain("system", activeMenuCodes);
        Assert.DoesNotContain("system:user", activeMenuCodes);
        Assert.DoesNotContain("ai", activeMenuCodes);
        Assert.DoesNotContain("ai:workbench", activeMenuCodes);
    }

    [Fact]
    public async Task SeedAsync_WithoutExplicitCapabilitiesDoesNotRestoreDeletedOptionalShellMenus()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = new SystemUserEntity
        {
            Id = "app-admin-user",
            DisplayName = "应用管理员",
            IsAdmin = true,
            PasswordHash = "hash",
            UserName = "app_admin"
        };
        var deletedWorkflow = CreateLegacyShellMenu("workflow", "审批流", null, null, null, "Directory", 8);
        deletedWorkflow.Visible = false;
        deletedWorkflow.IsDeleted = true;
        deletedWorkflow.DeletedBy = "previous-baseline";
        deletedWorkflow.DeletedTime = DateTime.UtcNow;
        await db.Insertable(deletedWorkflow).ExecuteCommandAsync();

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None);

        var workflowMenu = await db.Queryable<SystemMenuEntity>()
            .FirstAsync(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && item.MenuCode == "workflow");

        Assert.True(workflowMenu.IsDeleted);
        Assert.False(workflowMenu.Visible);
    }

    [Fact]
    public async Task SeedAsync_ExplicitEmptyCapabilitiesRetiresOptionalShellMenus()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = new SystemUserEntity
        {
            Id = "app-admin-user",
            DisplayName = "应用管理员",
            IsAdmin = true,
            PasswordHash = "hash",
            UserName = "app_admin"
        };
        await db.Insertable(new[]
        {
            CreateLegacyShellMenu("system", "系统设置", null, null, null, "Directory", 100),
            CreateLegacyShellMenu("system:user", "用户管理", "system", "/system/users", PermissionCodes.SystemUserQuery, "Menu", 101)
        }).ExecuteCommandAsync();

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None, "{\"shellCapabilities\":[]}");

        var activeMenuCodes = await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && !item.IsDeleted && item.Visible)
            .Select(item => item.MenuCode)
            .ToListAsync();

        Assert.DoesNotContain("system", activeMenuCodes);
        Assert.DoesNotContain("system:user", activeMenuCodes);
    }

    [Fact]
    public async Task SeedAsync_DefaultShellKeepsApplicationRuntimeMenusUnchanged()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = new SystemUserEntity
        {
            Id = "app-admin-user",
            DisplayName = "应用管理员",
            IsAdmin = true,
            PasswordHash = "hash",
            UserName = "app_admin"
        };
        var runtimeMenu = new SystemMenuEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = "tenant-a",
            AppCode = "MES",
            MenuName = "库存管理",
            MenuCode = "app-module:inventory",
            ParentCode = "dev-center",
            ScopeType = "ApplicationRuntime",
            ConfigJson = "{\"source\":\"designer\"}",
            MenuType = "Directory",
            SortOrder = 30,
            Visible = true,
            PermissionCode = PermissionCodes.BuildAppRuntimePagePermission("inventory", "view"),
            CreatedBy = "designer",
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        var runtimePageMenu = new SystemMenuEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = "tenant-a",
            AppCode = "MES",
            MenuName = "微流订单事务示例",
            MenuCode = "inventory-order-menu",
            ParentCode = "app-module:inventory",
            RoutePath = "/pages/inventory_order",
            ComponentName = "RuntimePage",
            PageCode = "inventory_order",
            ScopeType = "ApplicationRuntime",
            ConfigJson = "{\"source\":\"designer\"}",
            MenuType = "Menu",
            SortOrder = 31,
            Visible = true,
            PermissionCode = PermissionCodes.BuildAppRuntimePagePermission("inventory_order", "view"),
            CreatedBy = "designer",
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        await db.Insertable(new[] { runtimeMenu, runtimePageMenu }).ExecuteCommandAsync();

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None);

        var businessMenus = await db.Queryable<SystemMenuEntity>()
            .Where(item => item.TenantId == "tenant-a" && item.AppCode == "MES" && item.MenuCode.StartsWith("app-module:") && !item.IsDeleted)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .ToListAsync();
        var pageMenu = await db.Queryable<SystemMenuEntity>()
            .FirstAsync(item => item.MenuCode == "inventory-order-menu");

        Assert.Single(businessMenus);
        Assert.Equal("dev-center", businessMenus[0].ParentCode);
        Assert.Equal("ApplicationRuntime", businessMenus[0].ScopeType);
        Assert.False(pageMenu.IsDeleted);
        Assert.Equal("/pages/inventory_order", pageMenu.RoutePath);
        Assert.Equal("RuntimePage", pageMenu.ComponentName);
    }

    [Fact]
    public async Task ReadCountsAsync_CountsPersistedMenusWithoutDoubleCountingFixedShell()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = new SystemUserEntity
        {
            Id = "app-admin-user",
            DisplayName = "应用管理员",
            IsAdmin = true,
            PasswordHash = "hash",
            UserName = "app_admin"
        };
        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None);

        var reader = new ApplicationDatabaseCapabilityReader(NullLogger<ApplicationDatabaseCapabilityReader>.Instance);
        var counts = await reader.ReadCountsAsync(db, "tenant-a", "MES");

        Assert.Equal(5, counts.RootMenuCount);
        Assert.Equal(5, counts.MenuCount);
    }

    [Fact]
    public async Task ReadCountsAsync_UsesDesignerPagesInsteadOfLegacyPageSchemas()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var reader = new ApplicationDatabaseCapabilityReader(NullLogger<ApplicationDatabaseCapabilityReader>.Instance);
        // The legacy table is intentionally created only as a regression fixture;
        // RuntimeCore no longer creates or registers it for new databases.
        db.Ado.ExecuteCommand("""
CREATE TABLE system_page_schemas (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    PageCode TEXT NOT NULL,
    PageName TEXT NOT NULL,
    PageType TEXT NOT NULL DEFAULT 'custom',
    ModelCode TEXT NULL,
    PermissionCode TEXT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    Status TEXT NOT NULL DEFAULT 'Published',
    SchemaJson TEXT NOT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    CreatedTime TEXT NOT NULL
);
""");
        await db.Insertable(new ApplicationDevelopmentPageEntity
        {
            Id = "designer-page-count",
            TenantId = "tenant-a",
            AppCode = "MES",
            PageCode = "designer-page-count",
            PageName = "Designer page",
            Status = "Published",
            VersionId = "version-designer-page-count"
        }).ExecuteCommandAsync();

        var before = await reader.ReadCountsAsync(db, "tenant-a", "MES");
        await db.Ado.ExecuteCommandAsync(
            "INSERT INTO system_page_schemas (Id, TenantId, AppCode, PageCode, PageName, SchemaJson, CreatedTime) VALUES (@id, @tenant, @app, @code, @name, @schema, @created)",
            new SugarParameter("@id", "legacy-page-schema-count"),
            new SugarParameter("@tenant", "tenant-a"),
            new SugarParameter("@app", "MES"),
            new SugarParameter("@code", "legacy-page-schema-count"),
            new SugarParameter("@name", "Legacy schema"),
            new SugarParameter("@schema", "{}"),
            new SugarParameter("@created", DateTime.UtcNow));
        var after = await reader.ReadCountsAsync(db, "tenant-a", "MES");

        Assert.Equal(before.PageCount, after.PageCount);
        Assert.Equal(before.PublishedPageCount, after.PublishedPageCount);
        Assert.True(after.PageCount >= 1);
        Assert.True(after.PublishedPageCount >= 1);
    }

    [Fact]
    public async Task IsCurrentAsync_NonEmptyConfigWithoutShellCapabilitiesDoesNotForceReseed()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = CreateCurrentUser();
        const string configJson = "{\"database\":{\"provider\":\"Sqlite\",\"databaseName\":\"mes11.db\"}}";

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None, configJson);

        Assert.True(await seeder.IsCurrentAsync(db, "tenant-a", "MES", configJson, CancellationToken.None));
        Assert.True(await seeder.IsCurrentAsync(db, "tenant-a", "MES", "{\"database\":{\"provider\":\"Sqlite\",\"databaseName\":\"mes12.db\"}}", CancellationToken.None));
    }

    [Fact]
    public async Task IsCurrentAsync_ReturnsFalseWhenShellCapabilitiesChange()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = CreateCurrentUser();

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None, "{\"shellCapabilities\":[\"Workflow\"]}");

        Assert.True(await seeder.IsCurrentAsync(db, "tenant-a", "MES", "{\"shellCapabilities\":[\"workflow\"]}", CancellationToken.None));
        Assert.False(await seeder.IsCurrentAsync(db, "tenant-a", "MES", "{\"shellCapabilities\":[]}", CancellationToken.None));
    }

    [Fact]
    public async Task IsCurrentAsync_ReturnsFalseWhenRuntimeMenuRouteNeedsNormalization()
    {
        using var db = CreateDb();
        new ApplicationSystemAdministrationSchemaInitializer().Initialize(db);
        var seeder = CreateSeeder();
        await SeedRuntimePagePermissionsAsync(db);
        var currentUser = CreateCurrentUser();

        await seeder.SeedAsync(db, "tenant-a", "MES", currentUser, CancellationToken.None);
        await db.Insertable(new SystemMenuEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = "tenant-a",
            AppCode = "MES",
            MenuName = "旧运行页",
            MenuCode = "legacy-runtime",
            RoutePath = "/runtime/legacy_page",
            ComponentName = "RuntimePage",
            PageCode = "legacy_page",
            ScopeType = "ApplicationRuntime",
            MenuType = "Menu",
            SortOrder = 999,
            Visible = true,
            PermissionCode = PermissionCodes.BuildAppRuntimePagePermission("legacy_page", "view"),
            CreatedBy = currentUser.Id,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        }).ExecuteCommandAsync();

        Assert.False(await seeder.IsCurrentAsync(db, "tenant-a", "MES", null, CancellationToken.None));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private SqlSugarClient CreateDb() =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    private static ApplicationDatabaseBaselineSeeder CreateSeeder() =>
        new(
            new ApplicationRbacBaselineSeeder(),
            new ApplicationWorkflowAcceptanceBaselineSeeder(new PasswordHashService()),
            new ApplicationShellCapabilityResolver());

    private static SystemUserEntity CreateCurrentUser() =>
        new()
        {
            Id = "app-admin-user",
            DisplayName = "应用管理员",
            IsAdmin = true,
            PasswordHash = "hash",
            UserName = "app_admin"
        };

    private static async Task SeedRuntimePagePermissionsAsync(ISqlSugarClient db)
    {
        var permissions = new[]
        {
            PermissionCodes.BuildAppRuntimePagePermission("codex_microflow_order_demo", "view"),
            PermissionCodes.BuildAppRuntimePagePermission("codex_microflow_order_demo", "add"),
            PermissionCodes.BuildAppRuntimePagePermission("codex_microflow_order_demo", "edit")
        };
        var entities = permissions.Select(code => new SystemPermissionCodeEntity
        {
            Id = $"{code.Replace(':', '_')}_id",
            ModuleName = PermissionCodes.AppRuntimePageModuleName,
            PermissionCode = code,
            PermissionName = code,
            IsEnabled = true,
            CreatedBy = "test",
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        }).ToList();
        await db.Insertable(entities).ExecuteCommandAsync();
    }

    private static SystemMenuEntity CreateLegacyShellMenu(
        string menuCode,
        string menuName,
        string? parentCode,
        string? routePath,
        string? permissionCode,
        string menuType,
        int sortOrder) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = "tenant-a",
            AppCode = "MES",
            MenuName = menuName,
            MenuCode = menuCode,
            ParentCode = parentCode,
            RoutePath = routePath,
            ComponentName = routePath is null ? null : "ApplicationConsolePage",
            ScopeType = "ApplicationShell",
            ConfigJson = ApplicationShellMenuCatalog.FixedShellConfig(),
            MenuType = menuType,
            SortOrder = sortOrder,
            Visible = true,
            PermissionCode = permissionCode,
            CreatedBy = "legacy",
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
}
