using AsterERP.Api.Application.Platform.ApplicationPublishing;
using AsterERP.Api.Infrastructure.Abp.PlatformFoundation;
using AsterERP.Api.Infrastructure.Abp.PlatformPublish;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class PlatformPublishAbpModuleTests
{
    [Fact]
    public void Platform_publish_is_owned_by_abp()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpPlatformPublishModule)));
    }

    [Fact]
    public void Platform_publish_module_registers_real_services_and_migrator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        new AsterErpPlatformPublishModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPlatformApplicationPublishService) &&
            descriptor.ImplementationType == typeof(PlatformApplicationPublishService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(PlatformPublishSchemaMigrator));
    }

    [Fact]
    public async Task Schema_migrator_creates_publish_schema_idempotently()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:platform-publish-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var migrator = new PlatformPublishSchemaMigrator();
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        var tables = db.Ado.GetDataTable("SELECT name FROM sqlite_master WHERE type = 'table'")
            .Rows.Cast<System.Data.DataRow>()
            .Select(row => row["name"]?.ToString())
            .Where(name => name is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("system_application_publish_profiles", tables);
        Assert.Contains("system_application_publish_tasks", tables);
        Assert.Contains("system_application_publish_logs", tables);
        Assert.Contains("system_application_publish_artifacts", tables);
    }
}
