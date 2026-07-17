using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.DependencyInjection;
using AsterERP.Workflow.Persistence.Database;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowDependencyInjectionInitializationTests : IDisposable
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-workflow-di-{Guid.NewGuid():N}.db");

    [Fact]
    public void ResolvingProcessEngineConfiguration_DoesNotInitializePersistenceStore()
    {
        using var provider = BuildProvider();

        _ = provider.GetRequiredService<IProcessEngineConfiguration>();
        var db = provider.GetRequiredService<ISqlSugarClient>();

        Assert.Equal(0, db.Ado.GetInt(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ACT_GE_PROPERTY'"));
    }

    [Fact]
    public async Task PersistenceInitialization_RemainsExplicitlyAwaitable()
    {
        using var provider = BuildProvider();
        var configuration = provider.GetRequiredService<IProcessEngineConfiguration>();

        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkflowPersistenceStore>();
        await store.InitializeAsync(configuration);

        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        Assert.Equal(1, db.Ado.GetInt(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ACT_GE_PROPERTY'"));
    }

    public void Dispose()
    {
        if (File.Exists(databasePath))
        {
            try
            {
                File.Delete(databasePath);
            }
            catch (IOException)
            {
                // SqlSugar may release the native SQLite handle after the test fixture callback.
            }
        }
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SqlSugarScope>(_ => new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        }));
        services.AddSingleton<ISqlSugarClient>(serviceProvider =>
            serviceProvider.GetRequiredService<SqlSugarScope>());
        services.AddScoped<SqliteSchemaValidator>();
        services.AddScoped<DatabaseInitializer>();
        services.AddAsterERPWorkflow($"Data Source={databasePath}", DbType.Sqlite);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
