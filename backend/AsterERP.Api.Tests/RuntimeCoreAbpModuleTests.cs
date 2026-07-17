using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Abp.RuntimeCore;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class RuntimeCoreAbpModuleTests
{
    [Fact]
    public void Runtime_core_is_an_abp_module()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpRuntimeCoreModule)));
    }

    [Fact]
    public void Runtime_core_registers_application_services_and_schema_migrator_directly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        new AsterErpRuntimeCoreModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IRuntimePageSchemaService) &&
            descriptor.ImplementationType == typeof(RuntimePageSchemaService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IRuntimeDataModelService) &&
            descriptor.ImplementationType == typeof(RuntimeDataModelService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(RuntimeCoreSchemaMigrator));
    }

    [Fact]
    public void Runtime_core_registers_all_workspace_filter_entities()
    {
        var registry = new DataPermissionFilterRegistry();

        AsterErpRuntimeCoreModule.RegisterDataFilters(registry);

        Assert.Equal(
            new[]
            {
                typeof(SystemDataModelEntity),
                typeof(SystemTenantGridViewEntity),
                typeof(SystemUserGridViewEntity)
            }.OrderBy(type => type.FullName),
            registry.WorkspaceEntityTypes.OrderBy(type => type.FullName));
    }

    [Fact]
    public async Task Runtime_core_schema_migrator_is_idempotent_and_creates_all_runtime_tables()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:runtime-core-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var migrator = new RuntimeCoreSchemaMigrator();
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        var tables = db.Ado.GetDataTable("SELECT name FROM sqlite_master WHERE type = 'table'")
            .Rows.Cast<System.Data.DataRow>()
            .Select(row => row["name"]?.ToString())
            .Where(name => name is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("system_page_schemas", tables);
        Assert.Contains("system_data_models", tables);
        Assert.Contains("system_tenant_grid_views", tables);
        Assert.Contains("system_user_grid_views", tables);
    }
}
