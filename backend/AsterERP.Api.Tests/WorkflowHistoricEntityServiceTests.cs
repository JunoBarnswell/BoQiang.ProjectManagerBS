using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.History;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.DependencyInjection.Persistence;
using AsterERP.Workflow.Persistence.Database;
using AsterERP.Workflow.Persistence.Entities;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowHistoricEntityServiceTests : IDisposable
{
    private readonly string _mainDatabasePath = Path.Combine(Path.GetTempPath(), $"astererp-workflow-main-{Guid.NewGuid():N}.db");
    private readonly string _applicationDatabasePath = Path.Combine(Path.GetTempPath(), $"astererp-workflow-app-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task RecordProcessInstanceStartAsync_UsesWorkflowPersistenceStoreDatabase()
    {
        using var mainDb = CreateDb(_mainDatabasePath);
        using var applicationDb = CreateDb(_applicationDatabasePath);
        InitializeWorkflowSchema(mainDb);
        InitializeWorkflowSchema(applicationDb);

        var services = new ServiceCollection();
        services.AddSingleton<ISqlSugarClient>(mainDb);
        services.AddSingleton<IWorkflowPersistenceStore>(serviceProvider =>
            new SqlSugarWorkflowPersistenceStore(
                applicationDb,
                new DatabaseInitializer(applicationDb, new SqliteSchemaValidator(applicationDb)),
                serviceProvider));
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = ProcessEngineServiceProviderAccessor.Push(serviceProvider);

        var historicEntityService = new HistoricEntityServiceImplementation();

        await historicEntityService.RecordProcessInstanceStartAsync(
            "process-instance-app",
            "MES:test:1",
            "business-1",
            "wf_starter");

        Assert.Null(mainDb.Queryable<HistoricProcessInstanceEntity>().InSingle("process-instance-app"));
        var applicationHistory = applicationDb.Queryable<HistoricProcessInstanceEntity>().InSingle("process-instance-app");
        Assert.NotNull(applicationHistory);
        Assert.Equal("MES:test:1", applicationHistory.ProcessDefinitionId);
        Assert.Equal("business-1", applicationHistory.BusinessKey);
    }

    [Fact]
    public async Task HistoricProcessLifecycleAsync_RestoresReadsAndDeletesThroughCancellableChain()
    {
        using var database = CreateDb(_applicationDatabasePath);
        InitializeWorkflowSchema(database);

        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowPersistenceStore>(serviceProvider =>
            new SqlSugarWorkflowPersistenceStore(
                database,
                new DatabaseInitializer(database, new SqliteSchemaValidator(database)),
                serviceProvider));
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = ProcessEngineServiceProviderAccessor.Push(serviceProvider);

        var historicEntityService = new HistoricEntityServiceImplementation();
        await historicEntityService.RestoreHistoricProcessInstanceAsync(new HistoricProcessInstanceRecord
        {
            Id = "process-async-lifecycle",
            ProcessDefinitionId = "definition-1",
            BusinessKey = "business-1",
            StartUserId = "user-1"
        });

        var restored = await historicEntityService.GetHistoricProcessInstanceAsync("process-async-lifecycle");

        Assert.NotNull(restored);
        Assert.Equal("definition-1", restored.ProcessDefinitionId);
        Assert.True(await historicEntityService.DeleteHistoricProcessInstanceAsync("process-async-lifecycle"));
        Assert.Null(await historicEntityService.GetHistoricProcessInstanceAsync("process-async-lifecycle"));
    }

    [Fact]
    public async Task HistoricQueriesAsync_HonorCancellationToken()
    {
        using var database = CreateDb(_applicationDatabasePath);
        InitializeWorkflowSchema(database);

        var services = new ServiceCollection();
        services.AddSingleton<ISqlSugarClient>(database);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = ProcessEngineServiceProviderAccessor.Push(serviceProvider);

        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var historicEntityService = new HistoricEntityServiceImplementation();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            historicEntityService.GetHistoricProcessInstancesAsync(cancellationSource.Token));
    }

    public void Dispose()
    {
        TryDelete(_mainDatabasePath);
        TryDelete(_applicationDatabasePath);
    }

    private static SqlSugarClient CreateDb(string databasePath)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
    }

    private static void InitializeWorkflowSchema(ISqlSugarClient db)
    {
        new DatabaseInitializer(db, new SqliteSchemaValidator(db)).Initialize();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
