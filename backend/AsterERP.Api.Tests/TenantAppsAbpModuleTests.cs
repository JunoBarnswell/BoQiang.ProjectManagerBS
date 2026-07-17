using AsterERP.Api.Application.Tenant;
using AsterERP.Api.Application.Tenant.Apps;
using AsterERP.Api.Infrastructure.Abp.TenantApps;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class TenantAppsAbpModuleTests
{
    [Fact]
    public void Tenant_apps_is_owned_by_abp()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpTenantAppsModule)));
    }

    [Fact]
    public void Tenant_apps_module_registers_real_services_and_migrator()
    {
        var services = new ServiceCollection();
        new AsterErpTenantAppsModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(TenantAccessGuard));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ITenantAppService) &&
            descriptor.ImplementationType == typeof(TenantAppService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TenantAppsSchemaMigrator));
    }

    [Fact]
    public async Task Schema_migrator_creates_tenant_apps_schema_idempotently()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:tenant-apps-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var migrator = new TenantAppsSchemaMigrator();
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        var tableExists = db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'system_tenant_apps'");
        var indexExists = db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'idx_system_tenant_apps_unique'");

        Assert.Equal(1, tableExists);
        Assert.Equal(1, indexExists);
    }
}
