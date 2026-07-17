using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;

namespace AsterERP.Api.Tests;

/// <summary>
/// Owns a real application SQLite database and a real sandboxed SQLite data source.
/// It intentionally does not replace the provider, connection factory, repository, or audit writer.
/// </summary>
public sealed class ApplicationDataStudioSqliteFixture : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), $"astererp-data-studio-{Guid.NewGuid():N}");
    private readonly string applicationDatabasePath;
    private readonly string sourceDatabasePath;
    private readonly string keyRingPath;
    private SqliteStudioEnvironment? environment;

    public ApplicationDataStudioSqliteFixture()
    {
        applicationDatabasePath = Path.Combine(rootPath, "application.db");
        sourceDatabasePath = Path.Combine(rootPath, "data", "application-databases", "tenant-a", "MES", "studio.db");
        keyRingPath = Path.Combine(rootPath, "keys");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceDatabasePath)!);
        Directory.CreateDirectory(keyRingPath);

        CurrentUser = CreateCurrentUser();
        WorkspaceResolver = new ApplicationDataCenterWorkspaceResolver(CurrentUser);
        AppDb = CreateDb(applicationDatabasePath);
        AppDb.CodeFirst.InitTables<
            ApplicationDataSourceEntity,
            ApplicationDataSourceCatalogSnapshotEntity,
            ApplicationDataSourceSchemaChangePlanEntity,
            ApplicationDataObjectReferenceEntity,
            ApplicationDataCenterPublishedSnapshot>();
        AppDb.CodeFirst.InitTables<
            ApplicationQueryDatasetEntity,
            ApplicationDataEntityDefinitionEntity,
            ApplicationDataFieldDefinitionEntity,
            ApplicationIntegrationTaskEntity,
            ApplicationMicroflowEntity>();
        AppDb.CodeFirst.InitTables<ApplicationSqlScriptAuditEntity>();
        AppDb.CodeFirst.InitTables<ApplicationDataMutationLedgerEntity>();

        SecretProtector = new ApplicationDataSecretProtector(DataProtectionProvider.Create(keyRingPath));
        DatabaseAccessor = new FixedDatabaseAccessor(AppDb);
        PathApprovalService = new ApplicationDataSourceSqlitePathApprovalService(DatabaseAccessor, WorkspaceResolver, CurrentUser);
        environment = new SqliteStudioEnvironment(rootPath);
        Sandbox = new ApplicationDataSourceSqliteSandbox(environment, WorkspaceResolver, PathApprovalService);
        ConnectionFactory = new ApplicationDataSourceConnectionFactory(
            SecretProtector,
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance),
            Sandbox);
        ProviderRegistry = new ApplicationDataSourceProviderRegistry(
        [
            new SqliteApplicationDataSourceProvider(),
            new MySqlApplicationDataSourceProvider(),
            new PostgreSqlApplicationDataSourceProvider(),
            new SqlServerApplicationDataSourceProvider()
        ]);
        AuditWriter = new ApplicationDataCenterSqlScriptAuditWriter(
            DatabaseAccessor,
            WorkspaceResolver,
            CurrentUser,
            NullLogger<ApplicationDataCenterSqlScriptAuditWriter>.Instance);

        using var sourceDb = CreateDb(sourceDatabasePath);
        sourceDb.Ado.ExecuteCommand("CREATE TABLE dc_people (id INTEGER PRIMARY KEY, name TEXT NOT NULL, age INTEGER NULL)");
        sourceDb.Ado.ExecuteCommand("CREATE TABLE dc_composite (tenant TEXT NOT NULL, item_id INTEGER NOT NULL, quantity INTEGER NOT NULL, PRIMARY KEY (tenant, item_id))");
        sourceDb.Ado.ExecuteCommand("CREATE INDEX ix_dc_people_name ON dc_people(name)");
        sourceDb.Ado.ExecuteCommand("CREATE TRIGGER tr_dc_people_insert AFTER INSERT ON dc_people BEGIN UPDATE dc_people SET age = 0 WHERE id = NEW.id AND age IS NULL; END");
        sourceDb.Ado.ExecuteCommand("INSERT INTO dc_people (id, name, age) VALUES (1, 'Alice', 30)");
        sourceDb.Ado.ExecuteCommand("INSERT INTO dc_composite (tenant, item_id, quantity) VALUES ('tenant-a', 7, 2)");

        DataSourceId = "ds-studio-sqlite";
        AppDb.Insertable(new ApplicationDataSourceEntity
        {
            Id = DataSourceId,
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "studio_sqlite",
            ObjectName = "Studio SQLite",
            ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal,
            Endpoint = "studio.db",
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
            {
                ["databaseName"] = "studio.db",
                ["readOnly"] = false
            }),
            IsReadOnly = false
        }).ExecuteCommand();
    }

    public SqlSugarClient AppDb { get; }
    public ICurrentUser CurrentUser { get; }
    public ApplicationDataCenterWorkspaceResolver WorkspaceResolver { get; }
    public IWorkspaceDatabaseAccessor DatabaseAccessor { get; }
    public IApplicationDataSecretProtector SecretProtector { get; }
    public ApplicationDataSourceSqlitePathApprovalService PathApprovalService { get; }
    public ApplicationDataSourceSqliteSandbox Sandbox { get; }
    public ApplicationDataSourceConnectionFactory ConnectionFactory { get; }
    public ApplicationDataSourceProviderRegistry ProviderRegistry { get; }
    public ApplicationDataCenterSqlScriptAuditWriter AuditWriter { get; }
    public string DataSourceId { get; }
    public string RootPath => rootPath;
    public string ApplicationDatabasePath => applicationDatabasePath;
    public string SourceDatabasePath => sourceDatabasePath;
    public string SourceRelativePath => Path.Combine("data", "application-databases", "tenant-a", "MES", "studio.db");

    public ApplicationDataSourceCatalogService CreateCatalogService() =>
        new(DatabaseAccessor, WorkspaceResolver, ConnectionFactory, ProviderRegistry);

    public ApplicationDataSourceService CreateDataSourceService() =>
        new(
            new WorkspaceSqlSugarRepository<ApplicationDataSourceEntity>(DatabaseAccessor, CurrentUser),
            DatabaseAccessor,
            WorkspaceResolver,
            SecretProtector,
            new ApplicationDataCenterRiskGuard(),
            new ApplicationObjectReferenceService(DatabaseAccessor, WorkspaceResolver),
            new ApplicationDataCenterTemplateCatalog(),
            new ApplicationDataCenterPublishedSnapshotService(DatabaseAccessor, WorkspaceResolver),
            ConnectionFactory,
            new ApplicationDataPreviewReader(ProviderRegistry),
            ProviderRegistry,
            AuditWriter);

    public ApplicationDataSourceTableWorkbenchService CreateTableWorkbenchService() =>
        new(
            DatabaseAccessor,
            ConnectionFactory,
            CreateDataSourceService(),
            WorkspaceResolver,
            AuditWriter,
            ProviderRegistry);

    public ApplicationDataSourceViewWorkbenchService CreateViewWorkbenchService() =>
        new(
            DatabaseAccessor,
            WorkspaceResolver,
            ConnectionFactory,
            new ApplicationDataPreviewReader(ProviderRegistry),
            ProviderRegistry,
            AuditWriter);

    public ApplicationDataSourceEntity ReadDataSource() =>
        AppDb.Queryable<ApplicationDataSourceEntity>().Single(item => item.Id == DataSourceId);

    public static SqlSugarClient CreateDb(string path) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={path};Pooling=False",
        DbType = DbType.Sqlite,
        InitKeyType = InitKeyType.Attribute,
        IsAutoCloseConnection = true
    });

    public void Dispose()
    {
        AppDb.Ado.Close();
        AppDb.Dispose();
        environment = null;
        TryDelete(rootPath);
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "data-studio-test-user",
            "data-studio-test-user",
            "tenant-a",
            "Tenant A",
            "MES",
            "Tenant A MES",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            ["*"],
            "ALL",
            true,
            true,
            true,
            "test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(accessor));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class FixedDatabaseAccessor(SqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult<ISqlSugarClient>(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult<ISqlSugarClient>(db);
    }

    private sealed class FixedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class SqliteStudioEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "AsterERP.DataStudio.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
