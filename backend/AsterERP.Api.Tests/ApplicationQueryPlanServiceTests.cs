using System.Text.Json;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationQueryPlanServiceTests : IDisposable
{
    private readonly string appPath = Path.Combine(Path.GetTempPath(), $"query-plan-app-{Guid.NewGuid():N}.db");
    private readonly string sourcePath = Path.Combine(Path.GetTempPath(), $"query-plan-source-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ControlledUpdateExecutesTransactionallyAndWritesAudit()
    {
        using var appDb = CreateDb(appPath);
        var sourceDb = CreateDb(sourcePath);
        appDb.CodeFirst.InitTables<ApplicationDataSourceEntity, ApplicationDataSourceCatalogSnapshotEntity, ApplicationQueryDatasetEntity, ApplicationSqlScriptAuditEntity>();
        sourceDb.Ado.ExecuteCommand("CREATE TABLE orders (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        sourceDb.Ado.ExecuteCommand("INSERT INTO orders (id, name) VALUES (1, 'old')");
        await appDb.Insertable(new ApplicationDataSourceEntity
        {
            Id = "source-1", TenantId = "tenant-a", AppCode = "MES", ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "source", ObjectName = "source", ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal, Endpoint = sourcePath, ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?> { ["databaseName"] = sourcePath })
        }).ExecuteCommandAsync();
        await AddCatalogSnapshotAsync(appDb, "source-1");
        sourceDb.Dispose();

        var user = CreateCurrentUser();
        var accessor = new FixedDatabaseAccessor(appDb);
        var resolver = new ApplicationDataCenterWorkspaceResolver(user);
        var connectionFactory = new ApplicationDataSourceConnectionFactory(
            new TestHostEnvironment(Path.GetDirectoryName(sourcePath)!), new NoopProtector(),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance));
        var auditWriter = new ApplicationDataCenterSqlScriptAuditWriter(accessor, resolver, user, NullLogger<ApplicationDataCenterSqlScriptAuditWriter>.Instance);
        var service = new ApplicationQueryDatasetService(
            new WorkspaceSqlSugarRepository<ApplicationQueryDatasetEntity>(accessor, user), accessor, resolver,
            new NoopProtector(), new ApplicationDataCenterRiskGuard(), new ApplicationObjectReferenceService(accessor, resolver),
            new ApplicationDataCenterTemplateCatalog(), new ApplicationDataCenterPublishedSnapshotService(accessor, resolver),
            connectionFactory, user, CreateProviderRegistry(), new ApplicationQueryPlanCompiler(), auditWriter);
        var plan = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1", Nodes = [new("orders-node", TableResourceId("source-1", "orders"), "o")], AccessMode = ApplicationQueryPlanAccessMode.ControlledWrite,
            WriteOperation = ApplicationQueryPlanWriteOperation.Update,
            WriteValues = new Dictionary<string, string> { [ColumnResourceId("source-1", "orders", "name")] = "parameter:new-name" },
            Filters = [new(ColumnResourceId("source-1", "orders", "id"), "eq", "parameter:id", "orders-node")], Parameters = [new("parameter:id", "id", "int", 1), new("parameter:new-name", "newName", "string", "new")],
            ExpectedAffectedRows = 1, RowLimit = 10
        };
        var confirmation = await service.IssueRiskConfirmationAsync(new ApplicationQueryPlanRiskConfirmationRequest { Plan = plan });
        plan.RiskConfirmed = true;
        plan.RiskConfirmationId = confirmation.RiskConfirmationId;

        var response = await service.ExecuteControlledWriteQueryPlanAsync(plan);

        Assert.Equal(1, response.Total);
        using var verificationDb = CreateDb(sourcePath);
        Assert.Equal("new", verificationDb.Ado.GetString("SELECT name FROM orders WHERE id = 1"));
        var audit = await appDb.Queryable<ApplicationSqlScriptAuditEntity>().SingleAsync(item => item.SourceKind == "QueryPlan");
        Assert.True(audit.IsSuccess);
        Assert.Equal(1, audit.AffectedRows);
        Assert.Equal("UPDATE", audit.StatementSummary);

        await Assert.ThrowsAsync<ValidationException>(() => service.ExecuteControlledWriteQueryPlanAsync(plan));
    }

    [Fact]
    public async Task ControlledUpdate_BlocksBeforeDmlWhenAuditWriterIsMissing()
    {
        using var appDb = CreateDb(appPath);
        var sourceDb = CreateDb(sourcePath);
        appDb.CodeFirst.InitTables<ApplicationDataSourceEntity, ApplicationDataSourceCatalogSnapshotEntity, ApplicationQueryDatasetEntity, ApplicationSqlScriptAuditEntity>();
        sourceDb.Ado.ExecuteCommand("CREATE TABLE orders (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        sourceDb.Ado.ExecuteCommand("INSERT INTO orders (id, name) VALUES (1, 'old')");
        await appDb.Insertable(new ApplicationDataSourceEntity
        {
            Id = "source-audit-blocked", TenantId = "tenant-a", AppCode = "MES", ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "source_audit_blocked", ObjectName = "source audit blocked", ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal, Endpoint = sourcePath,
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?> { ["databaseName"] = sourcePath })
        }).ExecuteCommandAsync();
        await AddCatalogSnapshotAsync(appDb, "source-audit-blocked");
        sourceDb.Dispose();

        var user = CreateCurrentUser();
        var accessor = new FixedDatabaseAccessor(appDb);
        var resolver = new ApplicationDataCenterWorkspaceResolver(user);
        var connectionFactory = new ApplicationDataSourceConnectionFactory(
            new TestHostEnvironment(Path.GetDirectoryName(sourcePath)!), new NoopProtector(),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance));
        var service = new ApplicationQueryDatasetService(
            new WorkspaceSqlSugarRepository<ApplicationQueryDatasetEntity>(accessor, user), accessor, resolver,
            new NoopProtector(), new ApplicationDataCenterRiskGuard(), new ApplicationObjectReferenceService(accessor, resolver),
            new ApplicationDataCenterTemplateCatalog(), new ApplicationDataCenterPublishedSnapshotService(accessor, resolver),
            connectionFactory, user, CreateProviderRegistry());
        var plan = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-audit-blocked", Nodes = [new("orders-node", TableResourceId("source-audit-blocked", "orders"), "o")], AccessMode = ApplicationQueryPlanAccessMode.ControlledWrite,
            WriteOperation = ApplicationQueryPlanWriteOperation.Update,
            WriteValues = new Dictionary<string, string> { [ColumnResourceId("source-audit-blocked", "orders", "name")] = "parameter:new-name" },
            Filters = [new(ColumnResourceId("source-audit-blocked", "orders", "id"), "eq", "parameter:id", "orders-node")], Parameters = [new("parameter:id", "id", "int", 1), new("parameter:new-name", "newName", "string", "blocked")],
            ExpectedAffectedRows = 1, RowLimit = 10
        };
        var confirmation = await service.IssueRiskConfirmationAsync(new ApplicationQueryPlanRiskConfirmationRequest { Plan = plan });
        plan.RiskConfirmed = true;
        plan.RiskConfirmationId = confirmation.RiskConfirmationId;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.ExecuteControlledWriteQueryPlanAsync(plan));
        Assert.Contains("audit sink", exception.Message, StringComparison.OrdinalIgnoreCase);

        using var verificationDb = CreateDb(sourcePath);
        Assert.Equal("old", verificationDb.Ado.GetString("SELECT name FROM orders WHERE id = 1"));
    }

    [Fact]
    public async Task QueryPlan_RejectsDataSourceFromAnotherWorkspace()
    {
        using var appDb = CreateDb(appPath);
        appDb.CodeFirst.InitTables<ApplicationDataSourceEntity, ApplicationQueryDatasetEntity>();
        await appDb.Insertable(new ApplicationDataSourceEntity
        {
            Id = "foreign-source",
            TenantId = "tenant-b",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "foreign_source",
            ObjectName = "Foreign source",
            ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal,
            Endpoint = sourcePath,
            ConfigJson = "{}"
        }).ExecuteCommandAsync();

        var user = CreateCurrentUser();
        var accessor = new FixedDatabaseAccessor(appDb);
        var resolver = new ApplicationDataCenterWorkspaceResolver(user);
        var connectionFactory = new ApplicationDataSourceConnectionFactory(
            new TestHostEnvironment(Path.GetDirectoryName(sourcePath)!), new NoopProtector(),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance));
        var service = new ApplicationQueryDatasetService(
            new WorkspaceSqlSugarRepository<ApplicationQueryDatasetEntity>(accessor, user), accessor, resolver,
            new NoopProtector(), new ApplicationDataCenterRiskGuard(), new ApplicationObjectReferenceService(accessor, resolver),
            new ApplicationDataCenterTemplateCatalog(), new ApplicationDataCenterPublishedSnapshotService(accessor, resolver),
            connectionFactory, user, CreateProviderRegistry());

        var request = new ApplicationQueryPlanRequest
        {
            DataSourceId = "foreign-source",
            Nodes = [new("orders-node", TableResourceId("foreign-source", "orders"), "o")],
            Columns = [new(ColumnResourceId("foreign-source", "orders", "id"), NodeId: "orders-node")]
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteQueryPlanAsync(request, cancellation.Token));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ExecuteQueryPlanAsync(request));
    }

    [Fact]
    public async Task RuntimeQuery_RejectsLegacyTableConfiguration()
    {
        using var appDb = CreateDb(appPath);
        var sourceDb = CreateDb(sourcePath);
        appDb.CodeFirst.InitTables<
            ApplicationDataSourceEntity,
            ApplicationQueryDatasetEntity,
            ApplicationDataCenterPublishedSnapshot>();
        sourceDb.Ado.ExecuteCommand("CREATE TABLE orders (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        sourceDb.Ado.ExecuteCommand("INSERT INTO orders (id, name) VALUES (1, 'Alice')");
        await appDb.Insertable(new ApplicationDataSourceEntity
        {
            Id = "source-runtime",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "source_runtime",
            ObjectName = "Runtime source",
            ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal,
            Endpoint = sourcePath,
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?> { ["databaseName"] = sourcePath })
        }).ExecuteCommandAsync();
        await appDb.Insertable(new ApplicationQueryDatasetEntity
        {
            Id = "dataset-runtime",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.QueryDataset,
            ObjectCode = "dataset_runtime",
            ObjectName = "Runtime dataset",
            ObjectType = ApplicationQueryDatasetType.QueryView,
            Status = ApplicationDataCenterObjectStatus.Published,
            SourceObjectId = "source-runtime",
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?> { ["tableName"] = "main.orders" }),
            CreatedBy = "admin",
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        await appDb.Insertable(new ApplicationDataCenterPublishedSnapshot
        {
            Id = "source-runtime-snapshot",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectId = "source-runtime",
            ObjectCode = "source_runtime",
            ObjectType = ApplicationDataSourceType.Sqlite,
            VersionNo = 1,
            BindingJson = "{}",
            PublishedBy = "admin",
            PublishedAt = DateTime.UtcNow,
            CreatedBy = "admin",
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        await appDb.Insertable(new ApplicationDataCenterPublishedSnapshot
        {
            Id = "dataset-runtime-snapshot",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.QueryDataset,
            ObjectId = "dataset-runtime",
            ObjectCode = "dataset_runtime",
            ObjectType = ApplicationQueryDatasetType.QueryView,
            VersionNo = 1,
            BindingJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
            {
                ["sourceObjectId"] = "source-runtime",
                ["tableName"] = "main.orders"
            }),
            PublishedBy = "admin",
            PublishedAt = DateTime.UtcNow,
            CreatedBy = "admin",
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
        sourceDb.Dispose();

        var user = CreateCurrentUser();
        var accessor = new FixedDatabaseAccessor(appDb);
        var resolver = new ApplicationDataCenterWorkspaceResolver(user);
        var connectionFactory = new ApplicationDataSourceConnectionFactory(
            new TestHostEnvironment(Path.GetDirectoryName(sourcePath)!), new NoopProtector(),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance));
        var service = new ApplicationQueryDatasetService(
            new WorkspaceSqlSugarRepository<ApplicationQueryDatasetEntity>(accessor, user), accessor, resolver,
            new NoopProtector(), new ApplicationDataCenterRiskGuard(), new ApplicationObjectReferenceService(accessor, resolver),
            new ApplicationDataCenterTemplateCatalog(), new ApplicationDataCenterPublishedSnapshotService(accessor, resolver),
            connectionFactory, user, CreateProviderRegistry());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.QueryRuntimeAsync(
            "dataset-runtime",
            new ApplicationDataCenterRuntimeQueryRequest()));

        Assert.Contains("latest Resource ID QueryPlan", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationDataSourceProviderRegistry CreateProviderRegistry() =>
        new([
            new SqliteApplicationDataSourceProvider(),
            new MySqlApplicationDataSourceProvider(),
            new PostgreSqlApplicationDataSourceProvider(),
            new SqlServerApplicationDataSourceProvider()
        ]);

    private static async Task AddCatalogSnapshotAsync(SqlSugarClient db, string dataSourceId)
    {
        var table = new ApplicationDataSourceCatalogTableResponse(
            "orders",
            null,
            "TABLE",
            [
                new("id", "int", false, true, 0),
                new("name", "string", false, false, 1)
            ],
            [],
            [],
            []);
        await db.Insertable(new ApplicationDataSourceCatalogSnapshotEntity
        {
            Id = $"snapshot-{dataSourceId}",
            TenantId = "tenant-a",
            AppCode = "MES",
            DataSourceId = dataSourceId,
            Provider = ApplicationDataSourceType.Sqlite,
            SnapshotHash = "hash",
            VersionNo = 1,
            CapturedAt = DateTime.UtcNow,
            CatalogJson = JsonSerializer.Serialize(new[] { table })
        }).ExecuteCommandAsync();
    }

    private static string TableResourceId(string dataSourceId, string tableName) => ApplicationDataResourceId.Table(dataSourceId, null, tableName);

    private static string ColumnResourceId(string dataSourceId, string tableName, string columnName) => ApplicationDataResourceId.Column(TableResourceId(dataSourceId, tableName), columnName);

    public void Dispose()
    {
        TryDelete(appPath);
        TryDelete(sourcePath);
    }

    private static SqlSugarClient CreateDb(string path)
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={path}", DbType = DbType.Sqlite, InitKeyType = InitKeyType.Attribute, IsAutoCloseConnection = true
        });
        db.QueryFilter.AddTableFilter<ApplicationDataSourceEntity>(item => item.TenantId == "tenant-a" && item.AppCode == "MES");
        db.QueryFilter.AddTableFilter<ApplicationQueryDatasetEntity>(item => item.TenantId == "tenant-a" && item.AppCode == "MES");
        db.QueryFilter.AddTableFilter<ApplicationDataCenterPublishedSnapshot>(item => item.TenantId == "tenant-a" && item.AppCode == "MES");
        return db;
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin", "admin", "tenant-a", "Tenant", "MES", "MES", "root", "system-admin", ["admin"], ["admin"], ["*"], "ALL", true, true, true, "admin"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(accessor));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private sealed class FixedDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class NoopProtector : IApplicationDataSecretProtector
    {
        public string Protect(string plainText) => plainText;
        public string Unprotect(string cipherText) => cipherText;
        public string BuildPublicSecretSummary(string? cipherText) => "{}";
        public string BuildPublicSecretSummary(string? cipherText, string secretRef, DateTime? updatedAt) => "{}";
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "AsterERP.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
