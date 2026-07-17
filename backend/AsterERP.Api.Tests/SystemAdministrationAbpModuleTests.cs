using AsterERP.Api.Application.System.Dicts;
using AsterERP.Api.Application.System.Menus;
using AsterERP.Api.Application.System.Roles;
using AsterERP.Api.Application.System.Users;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Abp.SystemAdministration;
using AsterERP.Api.Infrastructure.Abp.AsterScene;
using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ApplicationDataCenter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class SystemAdministrationAbpModuleTests
{
    [Fact]
    public void AsterScene_is_an_abp_module_with_direct_services_and_migrators()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpAsterSceneModule)));

        var services = new ServiceCollection();
        new AsterErpAsterSceneModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AsterSceneSchemaMigrator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AsterSceneSeedService));
    }

    [Fact]
    public void Application_data_center_is_an_abp_module_with_direct_services_and_migrators()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpApplicationDataCenterModule)));

        var services = new ServiceCollection();
        new AsterErpApplicationDataCenterModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationDataCenterSchemaMigrator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationDataCenterSeedService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationDataSourceService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationDataSourceCatalogService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationDataSourceSqlitePathApprovalService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDataPermissionDescriptor<ApplicationDataSourceSqlitePathApprovalEntity>));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDataPermissionDescriptor<ApplicationDataSourceSqlitePathApprovalAuditEntity>));
    }

    [Fact]
    public async Task Application_data_center_schema_migrator_is_idempotent()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:application-data-center-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var migrator = new ApplicationDataCenterSchemaMigrator();
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'app_data_sources'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'idx_app_data_sources_workspace'"));
    }

    [Fact]
    public void System_administration_is_owned_by_abp()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpSystemAdministrationModule)));
    }

    [Fact]
    public void System_administration_module_registers_real_services_and_schema_migrator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        new AsterErpSystemAdministrationModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IDictManagementService) &&
            descriptor.ImplementationType == typeof(DictManagementService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISystemMenuService) &&
            descriptor.ImplementationType == typeof(SystemMenuService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISystemRoleService) &&
            descriptor.ImplementationType == typeof(SystemRoleService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISystemUserService) &&
            descriptor.ImplementationType == typeof(SystemUserService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(AsterErpSystemAdministrationSchemaMigrator));
    }

    [Fact]
    public async Task Schema_migrator_creates_system_administration_schema_idempotently()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:system-administration-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var services = new ServiceCollection();
        using var serviceProvider = services.BuildServiceProvider();
        var migrator = new AsterErpSystemAdministrationSchemaMigrator();

        await migrator.MigrateAsync(serviceProvider, db, CancellationToken.None);
        await migrator.MigrateAsync(serviceProvider, db, CancellationToken.None);

        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'system_dict_types'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'system_scheduled_jobs'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'idx_system_parameters_key'"));
    }

    [Fact]
    public void System_administration_permission_scope_remains_system_api_scoped()
    {
        var classifier = new DataPermissionRequestClassifier();

        Assert.True(classifier.IsSystemAdministrationApi("/api/system/users"));
        Assert.False(classifier.IsSystemAdministrationApi("/api/application-data-center/entities"));
    }
}
