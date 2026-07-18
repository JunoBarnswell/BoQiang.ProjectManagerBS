using System.Reflection;
using AsterERP.Api.Infrastructure.Abp.DevelopmentSeed;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Tests.Support;
using AsterERP.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class DevelopmentSeedDataServiceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"astererp-seed-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public void UpsertTenantApp_preserves_existing_config_json_when_seed_config_is_null()
    {
        const string originalConfigJson = "{\"applicationDatabase\":{\"provider\":\"Sqlite\",\"connectionStringCipherText\":\"cipher\",\"databaseName\":\"mes.db\"}}";
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemTenantAppEntity>();
        db.Insertable(new SystemTenantAppEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            Status = "Enabled",
            SystemName = "客户A MES",
            PrimaryColor = "#16a34a",
            ConfigJson = originalConfigJson
        }).ExecuteCommand();
        var service = new DevelopmentSeedDataService(db, NullLogger<DevelopmentSeedDataService>.Instance, new PasswordHashService(), Options.Create(new DevelopmentSeedOptions()));

        InvokeUpsertTenantApp(
            service,
            "tenant-a",
            "MES",
            "Enabled",
            "客户A MES",
            null,
            null,
            "#16a34a",
            null,
            null);

        var reloaded = db.Queryable<SystemTenantAppEntity>()
            .First(item => item.TenantId == "tenant-a" && item.AppCode == "MES");
        Assert.Equal(originalConfigJson, reloaded.ConfigJson);
        Assert.False(reloaded.IsDeleted);
    }

    [Fact]
    public void UpsertTenantApp_updates_config_json_when_seed_explicitly_provides_config()
    {
        const string originalConfigJson = "{\"applicationDatabase\":{\"provider\":\"Sqlite\",\"databaseName\":\"old.db\"}}";
        const string replacementConfigJson = "{\"applicationDatabase\":{\"provider\":\"Sqlite\",\"databaseName\":\"new.db\"}}";
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemTenantAppEntity>();
        db.Insertable(new SystemTenantAppEntity
        {
            TenantId = "tenant-a",
            AppCode = "MES",
            Status = "Enabled",
            SystemName = "客户A MES",
            ConfigJson = originalConfigJson
        }).ExecuteCommand();
        var service = new DevelopmentSeedDataService(db, NullLogger<DevelopmentSeedDataService>.Instance, new PasswordHashService(), Options.Create(new DevelopmentSeedOptions()));

        InvokeUpsertTenantApp(
            service,
            "tenant-a",
            "MES",
            "Enabled",
            "客户A MES",
            null,
            null,
            "#16a34a",
            null,
            replacementConfigJson);

        var reloaded = db.Queryable<SystemTenantAppEntity>()
            .First(item => item.TenantId == "tenant-a" && item.AppCode == "MES");
        Assert.Equal(replacementConfigJson, reloaded.ConfigJson);
    }

    [Fact]
    public void SeedDataModels_excludes_the_removed_page_schema_provider()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        var service = CreateService(db);

        InvokeSeedDataModels(service);

        var models = db.Queryable<SystemDataModelEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Published")
            .ToList();

        Assert.Equal(4, models.Count);
        Assert.All(models, model => Assert.Equal("runtime.menu", model.ModelCode));
        Assert.All(models, model => Assert.Equal("system.menus", model.ProviderKey));
        Assert.DoesNotContain(models, model => model.ProviderKey == "system.page-schemas");
    }

    [Fact]
    public void DevelopmentSeedModule_is_an_abp_module()
    {
        Assert.True(typeof(Volo.Abp.Modularity.AbpModule).IsAssignableFrom(typeof(AsterErpDevelopmentSeedModule)));
    }

    [Fact]
    public void DevelopmentSeedOptions_rejects_empty_passwords()
    {
        var result = new DevelopmentSeedOptionsValidator().Validate(
            Options.DefaultName,
            new DevelopmentSeedOptions
            {
                UserPasswords = new() { ["admin"] = " " }
            });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Platform_seed_registers_project_management_menu_and_grants_system_admin()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemPermissionCodeEntity, SystemRoleEntity, SystemRolePermissionEntity, SystemMenuEntity, WorkflowCategoryEntity>();
        var service = CreateService(db);

        InvokePrivateSeedStep(service, "SeedPermissionCodes");
        InvokePrivateSeedStep(service, "SeedRoles");
        InvokePrivateSeedStep(service, "SeedRolePermissions");
        InvokePrivateSeedStep(service, "SeedMenus");

        var projectManagementPermissionCodes = db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => !item.IsDeleted && item.ModuleName == "ProjectManagement")
            .Select(item => item.PermissionCode)
            .ToList();
        var systemAdmin = db.Queryable<SystemRoleEntity>()
            .First(item => item.TenantId == "tenant-system" && item.AppCode == "SYSTEM" && item.RoleCode == "admin" && !item.IsDeleted);
        var systemAdminPermissions = db.Queryable<SystemRolePermissionEntity, SystemPermissionCodeEntity>(
                (grant, permission) => grant.PermissionCodeId == permission.Id)
            .Where((grant, permission) => grant.RoleId == systemAdmin.Id && !grant.IsDeleted && !permission.IsDeleted)
            .Select((grant, permission) => permission.PermissionCode)
            .ToList();
        var projectManagementMenu = db.Queryable<SystemMenuEntity>()
            .First(item => item.TenantId == "tenant-system" && item.AppCode == "SYSTEM" && item.MenuCode == "project-management" && !item.IsDeleted);

        Assert.Equal(
            PermissionCodes.ProjectManagementPermissionCodes.OrderBy(item => item, StringComparer.Ordinal),
            projectManagementPermissionCodes.OrderBy(item => item, StringComparer.Ordinal));
        Assert.Contains(PermissionCodes.ProjectManagementProjectView, systemAdminPermissions);
        Assert.Equal("platform", projectManagementMenu.ParentCode);
        Assert.Equal("/project-management", projectManagementMenu.RoutePath);
        Assert.Equal(PermissionCodes.ProjectManagementProjectView, projectManagementMenu.PermissionCode);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    public void DevelopmentSeedModule_registers_seed_service_only_for_non_production_environments(
        string environmentName,
        bool shouldRegister)
    {
        var environment = new TestHostEnvironment(AppContext.BaseDirectory)
        {
            EnvironmentName = environmentName
        };
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        new AsterErpDevelopmentSeedModule().ConfigureServices(new Volo.Abp.Modularity.ServiceConfigurationContext(services));

        Assert.Equal(
            shouldRegister,
            services.Any(descriptor => descriptor.ServiceType == typeof(IDevelopmentSeedService)));
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

    private static DevelopmentSeedDataService CreateService(SqlSugarClient db) =>
        new(db, NullLogger<DevelopmentSeedDataService>.Instance, new PasswordHashService(), Options.Create(new DevelopmentSeedOptions()));

    private static void InvokeSeedDataModels(DevelopmentSeedDataService service)
    {
        var method = typeof(DevelopmentSeedDataService).GetMethod("SeedDataModels", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(service, null);
    }

    private static void InvokePrivateSeedStep(DevelopmentSeedDataService service, string methodName)
    {
        var method = typeof(DevelopmentSeedDataService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(service, null);
    }

    private static void InvokeUpsertTenantApp(
        DevelopmentSeedDataService service,
        string tenantId,
        string appCode,
        string status,
        string? systemName,
        string? logoFileId,
        string? faviconFileId,
        string? primaryColor,
        DateTime? expiredAt,
        string? configJson)
    {
        var method = typeof(DevelopmentSeedDataService).GetMethod("UpsertTenantApp", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(
            service,
            [
                tenantId,
                appCode,
                status,
                systemName,
                logoFileId,
                faviconFileId,
                primaryColor,
                expiredAt,
                configJson
            ]);
    }
}
