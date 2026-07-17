using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.Auth;
using AsterERP.Api.Application.Platform;
using AsterERP.Api.Application.Platform.Applications;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Platform;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class PlatformApplicationWorkspaceProvisioningTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"astererp-platform-app-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task CreateAsync_installs_new_application_for_current_platform_tenant_without_database_binding()
    {
        using var db = CreateDb();
        InitTables(db);
        await SeedPlatformWorkspaceAsync(db);
        var currentUser = CreatePlatformCurrentUser();
        var service = CreateApplicationService(db, currentUser);

        var response = await service.CreateAsync(new ApplicationUpsertRequest(
            "wms",
            "仓储管理",
            "Business",
            null,
            "/application-center",
            "/application-center",
            "/runtime",
            "Enabled",
            "1.0.0",
            null));

        Assert.Equal("WMS", response.AppCode);
        Assert.True(await db.Queryable<SystemApplicationEntity>()
            .AnyAsync(item => item.AppCode == "WMS" && !item.IsDeleted));

        var tenantApp = await db.Queryable<SystemTenantAppEntity>()
            .FirstAsync(item => item.TenantId == "tenant-a" && item.AppCode == "WMS" && !item.IsDeleted);
        Assert.Equal("Enabled", tenantApp.Status);
        Assert.Null(tenantApp.ConfigJson);

        var workspaceService = CreateWorkspaceTransitionService(db);
        var workspaces = await workspaceService.GetAvailableWorkspacesAsync("admin");
        var workspace = Assert.Single(workspaces, item => item.AppCode == "WMS");
        Assert.Equal("tenant-a", workspace.TenantId);
        Assert.False(workspace.IsDatabaseBound);
        Assert.True(workspace.CanManageInitialDatabaseBinding);
    }

    [Fact]
    public async Task GetBootstrapAsync_marks_unbound_application_as_manageable_for_platform_admin()
    {
        using var db = CreateDb();
        InitTables(db);
        await SeedPlatformWorkspaceAsync(db);
        await db.Insertable(new SystemApplicationEntity
        {
            Id = "app-wms",
            AppCode = "WMS",
            AppName = "仓储管理",
            AppType = "Business",
            Status = "Enabled"
        }).ExecuteCommandAsync();
        await db.Insertable(new SystemTenantAppEntity
        {
            Id = "tenant-app-wms",
            TenantId = "tenant-a",
            AppCode = "WMS",
            Status = "Enabled"
        }).ExecuteCommandAsync();

        var service = new ApplicationAuthService(
            db,
            CreatePlatformCurrentUser(),
            null!,
            null!,
            null!,
            CreateBindingResolver(),
            null!,
            null!,
            null!);

        var bootstrap = await service.GetBootstrapAsync("tenant-a", "WMS");

        Assert.False(bootstrap.DatabaseBinding.IsBound);
        Assert.False(bootstrap.DatabaseBinding.IsReachable);
        Assert.True(bootstrap.DatabaseBinding.CanManage);
    }

    public void Dispose()
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private PlatformApplicationService CreateApplicationService(ISqlSugarClient db, ICurrentUser currentUser)
    {
        var accessGuard = new PlatformAccessGuard(currentUser);
        return new PlatformApplicationService(
            db,
            accessGuard,
            new PlatformApplicationWorkspaceProvisioningService(db, currentUser, accessGuard));
    }

    private WorkspaceTransitionService CreateWorkspaceTransitionService(ISqlSugarClient db) =>
        new(
            db,
            null!,
            null!,
            null!,
            null!,
            CreateBindingResolver(),
            null!,
            null!);

    private static ApplicationDatabaseBindingResolver CreateBindingResolver() =>
        new(new PlainTextConnectionStringProtector(), new ApplicationManagedSqliteDatabaseResolver(new TestHostEnvironment()));

    private SqlSugarClient CreateDb() =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath};Pooling=False",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    private static void InitTables(ISqlSugarClient db)
    {
        db.CodeFirst.InitTables<
            SystemApplicationEntity,
            SystemTenantEntity,
            SystemTenantAppEntity,
            SystemUserEntity,
            SystemUserTenantMembershipEntity>();
    }

    private static async Task SeedPlatformWorkspaceAsync(ISqlSugarClient db)
    {
        await db.Insertable(new SystemTenantEntity
        {
            Id = "tenant-a",
            TenantCode = "tenant-a",
            TenantName = "客户A",
            Status = "Enabled"
        }).ExecuteCommandAsync();
        await db.Insertable(new SystemApplicationEntity
        {
            Id = "app-system",
            AppCode = "SYSTEM",
            AppName = "系统管理",
            AppType = "System",
            Status = "Enabled"
        }).ExecuteCommandAsync();
        await db.Insertable(new SystemTenantAppEntity
        {
            Id = "tenant-app-system",
            TenantId = "tenant-a",
            AppCode = "SYSTEM",
            Status = "Enabled"
        }).ExecuteCommandAsync();
        await db.Insertable(new SystemUserEntity
        {
            Id = "admin",
            UserName = "admin",
            DisplayName = "平台管理员",
            PasswordHash = "hash",
            IsAdmin = true,
            Status = "Enabled"
        }).ExecuteCommandAsync();
        await db.Insertable(new SystemUserTenantMembershipEntity
        {
            Id = "membership-admin",
            UserId = "admin",
            TenantId = "tenant-a",
            IsTenantAdmin = true,
            IsDefault = true,
            Status = "Enabled"
        }).ExecuteCommandAsync();
    }

    private static ICurrentUser CreatePlatformCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-a",
            "客户A",
            "SYSTEM",
            "系统管理",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            ["*"],
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }

    private sealed class PlainTextConnectionStringProtector : IApplicationConnectionStringProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AsterERP.Api.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = null!;

        public string WebRootPath { get; set; } = Path.GetTempPath();

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
