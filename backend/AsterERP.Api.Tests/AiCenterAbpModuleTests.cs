using AsterERP.Api.Infrastructure.Abp.AiCenter;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AiCenterAbpModuleTests
{
    [Fact]
    public void AiCenter_is_an_abp_module_with_direct_services_and_migrators()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpAiCenterModule)));
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        new AsterErpAiCenterModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AiCenterSchemaMigrator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(AiCenterModuleSeeder));
    }

    [Fact]
    public async Task Schema_migrator_is_idempotent()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:ai-center-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var migrator = new AiCenterSchemaMigrator();
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'ai_providers'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'ai_flowise_chat_flows'"));
    }

    [Fact]
    public void AiCenter_registers_workspace_and_owned_data_filters()
    {
        var registry = new DataPermissionFilterRegistry();

        AsterErpAiCenterModule.RegisterDataFilters(registry);

        Assert.NotEmpty(registry.AiWorkspaceEntityTypes);
        Assert.NotEmpty(registry.AiOwnedEntityTypes);
        Assert.Contains(registry.AiWorkspaceEntityTypes, type => type.Name == nameof(AsterERP.Api.Modules.Ai.AiConversationEntity));
        Assert.Contains(registry.AiOwnedEntityTypes, type => type.Name == nameof(AsterERP.Api.Modules.Ai.AiConversationEntity));
    }
}
