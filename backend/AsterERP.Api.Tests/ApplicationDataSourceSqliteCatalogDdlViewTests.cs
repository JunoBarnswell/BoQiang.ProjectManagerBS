using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Tests.Support;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceSqliteCatalogDdlViewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"astererp-data-studio-{Guid.NewGuid():N}");
    private const string DataSourceId = "sqlite-e2e-source";

    [Fact]
    public async Task CatalogRefresh_ReadsRealSqliteKeysIndexesTriggersAndNodeChanges()
    {
        using var appDb = CreateAppDb();
        await SeedSourceAsync();
        var catalog = CreateCatalogService(appDb);

        var first = await catalog.RefreshAsync(DataSourceId);
        var orders = Assert.Single(first.Tables, item => item.TableName == "orders");

        Assert.Equal(1, first.VersionNo);
        Assert.Equal(["tenant", "order_id"], orders.Columns.Where(item => item.PrimaryKey).OrderBy(item => item.Order).Select(item => item.ColumnName));
        Assert.Contains(orders.Indexes, item => item.Name == "ix_orders_status");
        Assert.DoesNotContain(orders.Indexes, item => item.Name.StartsWith("sqlite_autoindex_", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(orders.Constraints, item => item.Name.StartsWith("sqlite_autoindex_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(orders.Triggers, item => item.Name == "tr_orders_touch");
        Assert.NotEmpty(first.SnapshotHash);

        using (var source = OpenSourceDb())
        {
            source.Ado.ExecuteCommand("ALTER TABLE orders ADD COLUMN note TEXT NULL");
        }

        var refreshed = await catalog.RefreshNodeAsync(DataSourceId, new ApplicationDataSourceCatalogRefreshRequest(null, "orders"));
        var refreshedOrders = Assert.Single(refreshed.Tables, item => item.TableName == "orders");

        Assert.Equal(2, refreshed.VersionNo);
        Assert.Equal(first.SnapshotHash, refreshed.PreviousSnapshotHash);
        Assert.Contains(refreshedOrders.Columns, item => item.ColumnName == "note");
        Assert.Contains(refreshed.Changes, item => item.ChangeType == "Added" && item.NodeName == "note");
        Assert.Equal(refreshed.SnapshotHash, (await catalog.GetLatestAsync(DataSourceId))!.SnapshotHash);
    }

    [Fact]
    public async Task CatalogRefresh_IgnoresSnapshotsFromAnotherWorkspace()
    {
        using var appDb = CreateAppDb();
        await SeedSourceAsync();
        var catalog = CreateCatalogService(appDb);

        var first = await catalog.RefreshAsync(DataSourceId);
        await appDb.Insertable(new ApplicationDataSourceCatalogSnapshotEntity
        {
            Id = "foreign-catalog-snapshot",
            TenantId = "tenant-b",
            AppCode = "MES",
            DataSourceId = DataSourceId,
            Provider = ApplicationDataSourceType.Sqlite,
            SnapshotHash = "foreign",
            VersionNo = 999,
            CapturedAt = DateTime.UtcNow.AddMinutes(1),
            CatalogJson = "[]",
            ChangeJson = "[]"
        }).ExecuteCommandAsync();

        var refreshed = await catalog.RefreshNodeAsync(DataSourceId, new ApplicationDataSourceCatalogRefreshRequest(null, "orders"));

        Assert.Equal(first.VersionNo + 1, refreshed.VersionNo);
        Assert.Equal(first.SnapshotHash, refreshed.PreviousSnapshotHash);
        Assert.Equal(refreshed.SnapshotHash, (await catalog.GetLatestAsync(DataSourceId))!.SnapshotHash);
    }

    [Fact]
    public async Task TableSchemaChangePlan_DeploysRealSqliteTableAndAuditsAppliedStatus()
    {
        using var appDb = CreateAppDb();
        await SeedSourceAsync();
        var tableWorkbench = CreateTableWorkbench(appDb);
        var request = new ApplicationDataSourceCreateTableRequest(
            "planned_items", null, "Planned items", null,
            [
                new("id", "INTEGER", false, true, null, null),
                new("description", "TEXT", true, false, null, null)
            ]);

        var plan = await tableWorkbench.CreateTablePlanAsync(DataSourceId, request);

        Assert.Equal("CreateTable", plan.Operation);
        Assert.True(plan.RequiresConfirmation);
        Assert.True(plan.Reversible);
        Assert.Null(plan.EstimatedAffectedRows);
        Assert.Contains("CREATE TABLE", plan.SqlPreview, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(plan.Risks);

        var detail = await tableWorkbench.DeployTablePlanAsync(
            DataSourceId,
            new ApplicationDataSourceSchemaChangePlanRequest(plan.PlanHash, request, true));

        Assert.Equal("planned_items", detail.Table.TableName);
        using var source = OpenSourceDb();
        Assert.Equal(1, source.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'planned_items'"));
        var persistedPlan = await appDb.Queryable<ApplicationDataSourceSchemaChangePlanEntity>().Where(item => item.PlanHash == plan.PlanHash).SingleAsync();
        Assert.Equal("Applied", persistedPlan.Status);
        Assert.Null(persistedPlan.EstimatedAffectedRows);
        Assert.Equal("Unknown", persistedPlan.EstimatedAffectedRowsStatus);
        var audit = await appDb.Queryable<ApplicationSqlScriptAuditEntity>().Where(item => item.SourceKind == "SchemaChangePlan").SingleAsync();
        Assert.True(audit.IsSuccess);
        Assert.Equal("Succeeded", audit.Outcome);
        Assert.Equal(plan.PlanHash, audit.RequestHash);
        Assert.Equal("tenant-a", audit.TenantId);
        Assert.Equal("MES", audit.AppCode);
        Assert.False(string.IsNullOrWhiteSpace(audit.TraceId));
        Assert.Equal("admin", audit.ActorUserId);
        Assert.DoesNotContain("password", audit.RedactedDetailsJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditAvailabilityCheckIsReadOnlyAndAuditWriteFailureIsNotSwallowed()
    {
        using var appDb = CreateAppDb();
        var auditWriter = CreateAuditWriter(appDb);
        var before = await appDb.Queryable<ApplicationSqlScriptAuditEntity>().CountAsync();

        await auditWriter.EnsureAvailableAsync(CancellationToken.None);

        Assert.Equal(before, await appDb.Queryable<ApplicationSqlScriptAuditEntity>().CountAsync());
        BlockAuditInsert(appDb);
        await Assert.ThrowsAnyAsync<Exception>(() => auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
        {
            TraceId = "audit-write-failure",
            SourceKind = "Test",
            ScriptHash = "test",
            ScriptPreview = "test",
            StatementSummary = "TEST",
            Operation = "test",
            ResourceKind = "test",
            IsSuccess = true
        }, CancellationToken.None));
        Assert.Equal(before, await appDb.Queryable<ApplicationSqlScriptAuditEntity>().CountAsync());
    }

    [Fact]
    public async Task TableSchemaChangePlan_RequiresConfirmationAndFailedDuplicateLeavesAuditEvidence()
    {
        using var appDb = CreateAppDb();
        await SeedSourceAsync();
        var tableWorkbench = CreateTableWorkbench(appDb);
        var request = new ApplicationDataSourceCreateTableRequest(
            "duplicate_items", null, "Duplicate items", null,
            [new("id", "INTEGER", false, true, null, null)]);
        var plan = await tableWorkbench.CreateTablePlanAsync(DataSourceId, request);

        await Assert.ThrowsAsync<ValidationException>(() => tableWorkbench.DeployTablePlanAsync(
            DataSourceId,
            new ApplicationDataSourceSchemaChangePlanRequest(plan.PlanHash, request, false)));
        Assert.Equal(0, await appDb.Queryable<ApplicationSqlScriptAuditEntity>().CountAsync());

        await tableWorkbench.DeployTablePlanAsync(DataSourceId, new ApplicationDataSourceSchemaChangePlanRequest(plan.PlanHash, request, true));
        await Assert.ThrowsAnyAsync<Exception>(() => tableWorkbench.DeployTablePlanAsync(DataSourceId, new ApplicationDataSourceSchemaChangePlanRequest(plan.PlanHash, request, true)));

        using var source = OpenSourceDb();
        Assert.Equal(1, source.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'duplicate_items'"));
        var audits = await appDb.Queryable<ApplicationSqlScriptAuditEntity>().Where(item => item.SourceKind == "SchemaChangePlan").ToListAsync();
        Assert.Contains(audits, item => item.IsSuccess && item.Outcome == "Succeeded");
        Assert.Contains(audits, item => !item.IsSuccess && item.Outcome == "Failed");
        var failedPlan = await appDb.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .SingleAsync(item => item.PlanHash == plan.PlanHash);
        Assert.Equal("Failed", failedPlan.Status);
    }

    [Fact]
    public async Task NonTransactionalDdlFailurePersistsManualRecoveryAndAuditEvidence()
    {
        using var appDb = CreateAppDb();
        await SeedSourceAsync();
        using (var source = OpenSourceDb())
        {
            source.Ado.ExecuteCommand("CREATE TABLE non_transactional_existing (id INTEGER PRIMARY KEY)");
        }

        var tableWorkbench = CreateTableWorkbench(appDb, new NonTransactionalSqliteProvider());
        var request = new ApplicationDataSourceCreateTableRequest(
            "non_transactional_existing",
            null,
            "Non transactional existing",
            null,
            [new("id", "INTEGER", false, true, null, null)]);
        var plan = await tableWorkbench.CreateTablePlanAsync(DataSourceId, request);

        Assert.False(plan.Reversible);
        Assert.Contains(plan.Risks, risk => risk.Contains("ManualRecovery", StringComparison.OrdinalIgnoreCase));

        await Assert.ThrowsAnyAsync<Exception>(() => tableWorkbench.DeployTablePlanAsync(
            DataSourceId,
            new ApplicationDataSourceSchemaChangePlanRequest(plan.PlanHash, request, true)));

        var persisted = await appDb.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .SingleAsync(item => item.PlanHash == plan.PlanHash);
        Assert.Equal("ManualRecovery", persisted.Status);
        var audit = await appDb.Queryable<ApplicationSqlScriptAuditEntity>()
            .SingleAsync(item => item.SourceId == plan.PlanId);
        Assert.False(audit.IsSuccess);
        Assert.Equal("ManualRecovery", audit.Outcome);
        Assert.Equal("ExternalDdlOutcomeUnknown", audit.FailureCode);
    }

    [Fact]
    public void TableWorkbench_RequiresProviderRegistryDependency()
    {
        var parameter = typeof(ApplicationDataSourceTableWorkbenchService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Single(item => item.ParameterType == typeof(ApplicationDataSourceProviderRegistry));

        Assert.False(parameter.IsOptional);
        Assert.False(parameter.HasDefaultValue);
    }

    [Fact]
    public async Task MySqlCreateTablePlanIsNotMarkedReversible()
    {
        using var appDb = CreateAppDb();
        await appDb.Insertable(new ApplicationDataSourceEntity
        {
            Id = "mysql-plan-source",
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "mysql_plan_source",
            ObjectName = "MySQL plan source",
            ObjectType = ApplicationDataSourceType.MySql,
            Status = ApplicationDataCenterObjectStatus.Normal,
            ConfigJson = "{}",
            IsReadOnly = false
        }).ExecuteCommandAsync();

        var request = new ApplicationDataSourceCreateTableRequest(
            "mysql_planned_items",
            null,
            "MySQL planned items",
            null,
            [new("id", "INTEGER", false, true, null, null)]);
        var plan = await CreateTableWorkbench(appDb).CreateTablePlanAsync("mysql-plan-source", request);

        Assert.False(plan.Reversible);
        Assert.Contains(plan.Risks, risk => risk.Contains("ManualRecovery", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ViewWorkbench_UsesCandidateValidationAndCompensationOnInvalidReplacement()
    {
        using var appDb = CreateAppDb();
        await SeedSourceAsync();
        var views = CreateViewWorkbench(appDb);
        var created = await views.CreateAsync(DataSourceId, new ApplicationDataSourceViewUpsertRequest("orders_view", null, "Orders", "SELECT tenant, status FROM orders", null));
        var createAudit = await appDb.Queryable<ApplicationSqlScriptAuditEntity>()
            .Where(item => item.SourceKind == "DataSourceView" && item.Operation == "view.create")
            .SingleAsync();
        Assert.Equal(PermissionCodes.AppDataCenterQueryDatasetAdd, createAudit.PermissionCode);

        using (var source = OpenSourceDb())
        {
            Assert.Equal("open", source.Ado.GetString("SELECT status FROM orders_view WHERE tenant = 'tenant-a'"));
            Assert.Equal(0, source.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'view' AND name LIKE 'orders_view_candidate_%'"));
        }

        await Assert.ThrowsAnyAsync<Exception>(() => views.UpdateAsync(
            DataSourceId,
            created.Id,
            new ApplicationDataSourceViewUpsertRequest("orders_view", null, "Broken", "SELECT missing_column FROM orders", null)));

        using (var source = OpenSourceDb())
        {
            Assert.Equal("open", source.Ado.GetString("SELECT status FROM orders_view WHERE tenant = 'tenant-a'"));
        }

        var persisted = await appDb.Queryable<ApplicationQueryDatasetEntity>().Where(item => item.Id == created.Id).SingleAsync();
        Assert.Equal("SELECT tenant, status FROM orders", persisted.ViewSql);
        var audit = await appDb.Queryable<ApplicationSqlScriptAuditEntity>().Where(item => item.SourceKind == "DataSourceView").OrderByDescending(item => item.CreatedTime).FirstAsync();
        Assert.False(audit.IsSuccess);
        Assert.Equal("ManualRecovery", audit.Outcome);
        Assert.Equal("{\"sqlStored\":false}", audit.RedactedDetailsJson);
        Assert.Equal(PermissionCodes.AppDataCenterQueryDatasetEdit, audit.PermissionCode);
    }

    [Fact]
    public async Task ViewWorkbench_CreateFailureWritesFailureAudit()
    {
        using var appDb = CreateAppDb();
        await SeedSourceAsync();
        using (var source = OpenSourceDb())
        {
            source.Ado.ExecuteCommand("CREATE VIEW existing_view AS SELECT tenant FROM orders");
        }

        var views = CreateViewWorkbench(appDb);
        await Assert.ThrowsAnyAsync<Exception>(() => views.CreateAsync(
            DataSourceId,
            new ApplicationDataSourceViewUpsertRequest("existing_view", null, "Existing", "SELECT tenant FROM orders", null)));

        var audit = await appDb.Queryable<ApplicationSqlScriptAuditEntity>()
            .Where(item => item.SourceKind == "DataSourceView")
            .SingleAsync();
        Assert.False(audit.IsSuccess);
        Assert.Equal("ManualRecovery", audit.Outcome);
        Assert.Equal(DataSourceId, audit.SourceId);
        Assert.Equal(PermissionCodes.AppDataCenterQueryDatasetAdd, audit.PermissionCode);
    }

    [Fact]
    public async Task ViewWorkbench_RejectsSchemaWhenProviderDoesNotSupportSchemas()
    {
        using var appDb = CreateAppDb();
        await SeedSourceAsync();
        var views = CreateViewWorkbench(appDb);

        await Assert.ThrowsAsync<ValidationException>(() => views.CreateAsync(
            DataSourceId,
            new ApplicationDataSourceViewUpsertRequest("orders_view", "main", "Orders", "SELECT tenant, status FROM orders", null)));

        using var source = OpenSourceDb();
        Assert.Equal(0, source.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'view' AND name = 'orders_view'"));

        var created = await views.CreateAsync(
            DataSourceId,
            new ApplicationDataSourceViewUpsertRequest("orders_view", null, "Orders", "SELECT tenant, status FROM orders", null));
        await Assert.ThrowsAsync<ValidationException>(() => views.UpdateAsync(
            DataSourceId,
            created.Id,
            new ApplicationDataSourceViewUpsertRequest("orders_view", "main", "Orders", "SELECT tenant, status FROM orders", null)));

        Assert.Equal("SELECT tenant, status FROM orders", (await appDb.Queryable<ApplicationQueryDatasetEntity>()
            .Where(item => item.Id == created.Id)
            .SingleAsync()).ViewSql);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
        catch (IOException)
        {
        }
    }

    private async Task SeedSourceAsync()
    {
        var sourcePath = GetSourcePath();
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        using var source = OpenSourceDb();
        source.Ado.ExecuteCommand("CREATE TABLE orders (tenant TEXT NOT NULL, order_id INTEGER NOT NULL, status TEXT NOT NULL, version INTEGER NOT NULL, PRIMARY KEY (tenant, order_id))");
        source.Ado.ExecuteCommand("CREATE INDEX ix_orders_status ON orders(status)");
        source.Ado.ExecuteCommand("CREATE TRIGGER tr_orders_touch AFTER UPDATE ON orders BEGIN UPDATE orders SET version = version + 1 WHERE tenant = NEW.tenant AND order_id = NEW.order_id; END");
        source.Ado.ExecuteCommand("INSERT INTO orders (tenant, order_id, status, version) VALUES ('tenant-a', 1, 'open', 1)");
    }

    private SqlSugarClient CreateAppDb()
    {
        Directory.CreateDirectory(root);
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={Path.Combine(root, "app.db")}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
        db.QueryFilter.AddTableFilter<ApplicationDataSourceEntity>(item => item.TenantId == "tenant-a" && item.AppCode == "MES");
        db.QueryFilter.AddTableFilter<ApplicationDataSourceCatalogSnapshotEntity>(item => item.TenantId == "tenant-a" && item.AppCode == "MES");
        db.QueryFilter.AddTableFilter<ApplicationDataCenterPublishedSnapshot>(item => item.TenantId == "tenant-a" && item.AppCode == "MES");
        db.CodeFirst.InitTables<ApplicationDataSourceEntity, ApplicationDataSourceCatalogSnapshotEntity, ApplicationDataSourceSchemaChangePlanEntity, ApplicationQueryDatasetEntity, ApplicationSqlScriptAuditEntity>();
        db.CodeFirst.InitTables<ApplicationDataCenterPublishedSnapshot>();
        db.Insertable(new ApplicationDataSourceEntity
        {
            Id = DataSourceId,
            TenantId = "tenant-a",
            AppCode = "MES",
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "sqlite_e2e",
            ObjectName = "SQLite E2E",
            ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal,
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?> { ["databaseName"] = "source.db" }),
            IsReadOnly = false
        }).ExecuteCommand();
        return db;
    }

    private SqlSugarClient OpenSourceDb() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={GetSourcePath()}",
        DbType = DbType.Sqlite,
        InitKeyType = InitKeyType.Attribute,
        IsAutoCloseConnection = true
    });

    private string GetSourcePath() => Path.Combine(root, "data", "application-databases", "tenant-a", "MES", "source.db");

    private ApplicationDataSourceCatalogService CreateCatalogService(ISqlSugarClient appDb)
    {
        var currentUser = CreateCurrentUser();
        var accessor = new FixedWorkspaceDatabaseAccessor(appDb);
        return new ApplicationDataSourceCatalogService(accessor, new ApplicationDataCenterWorkspaceResolver(currentUser), CreateConnectionFactory(appDb, currentUser), CreateProviderRegistry());
    }

    private ApplicationDataSourceTableWorkbenchService CreateTableWorkbench(
        ISqlSugarClient appDb,
        IApplicationDataSourceProvider? providerOverride = null)
    {
        var currentUser = CreateCurrentUser();
        var accessor = new FixedWorkspaceDatabaseAccessor(appDb);
        var resolver = new ApplicationDataCenterWorkspaceResolver(currentUser);
        var connectionFactory = CreateConnectionFactory(appDb, currentUser);
        var dataSourceService = CreateDataSourceService(appDb, currentUser, connectionFactory, resolver, accessor);
        var auditWriter = new ApplicationDataCenterSqlScriptAuditWriter(accessor, resolver, currentUser, NullLogger<ApplicationDataCenterSqlScriptAuditWriter>.Instance);
        return new ApplicationDataSourceTableWorkbenchService(accessor, connectionFactory, dataSourceService, resolver, auditWriter, CreateProviderRegistry(providerOverride));
    }

    private ApplicationDataSourceViewWorkbenchService CreateViewWorkbench(ISqlSugarClient appDb)
    {
        var currentUser = CreateCurrentUser();
        var accessor = new FixedWorkspaceDatabaseAccessor(appDb);
        var resolver = new ApplicationDataCenterWorkspaceResolver(currentUser);
        var connectionFactory = CreateConnectionFactory(appDb, currentUser);
        var auditWriter = new ApplicationDataCenterSqlScriptAuditWriter(accessor, resolver, currentUser, NullLogger<ApplicationDataCenterSqlScriptAuditWriter>.Instance);
        return new ApplicationDataSourceViewWorkbenchService(accessor, resolver, connectionFactory, new ApplicationDataPreviewReader(CreateProviderRegistry()), CreateProviderRegistry(), auditWriter);
    }

    private ApplicationDataCenterSqlScriptAuditWriter CreateAuditWriter(ISqlSugarClient appDb)
    {
        var currentUser = CreateCurrentUser();
        var accessor = new FixedWorkspaceDatabaseAccessor(appDb);
        return new ApplicationDataCenterSqlScriptAuditWriter(
            accessor,
            new ApplicationDataCenterWorkspaceResolver(currentUser),
            currentUser,
            NullLogger<ApplicationDataCenterSqlScriptAuditWriter>.Instance);
    }

    private ApplicationDataSourceService CreateDataSourceService(ISqlSugarClient appDb, ICurrentUser currentUser, ApplicationDataSourceConnectionFactory connectionFactory, ApplicationDataCenterWorkspaceResolver resolver, FixedWorkspaceDatabaseAccessor accessor) =>
        new(
            new WorkspaceSqlSugarRepository<ApplicationDataSourceEntity>(accessor, currentUser), accessor, resolver,
            new NoopProtector(), new ApplicationDataCenterRiskGuard(), new ApplicationObjectReferenceService(accessor, resolver),
            new ApplicationDataCenterTemplateCatalog(), new ApplicationDataCenterPublishedSnapshotService(accessor, resolver),
            connectionFactory, new ApplicationDataPreviewReader(CreateProviderRegistry()), CreateProviderRegistry());

    private ApplicationDataSourceConnectionFactory CreateConnectionFactory(ISqlSugarClient appDb, ICurrentUser currentUser)
    {
        var resolver = new ApplicationDataCenterWorkspaceResolver(currentUser);
        var approval = new ApplicationDataSourceSqlitePathApprovalService(new FixedWorkspaceDatabaseAccessor(appDb), resolver, currentUser);
        var sandbox = new ApplicationDataSourceSqliteSandbox(new TestHostEnvironment(root), resolver, approval);
        return new ApplicationDataSourceConnectionFactory(new NoopProtector(), new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance), sandbox);
    }

    private static ApplicationDataSourceProviderRegistry CreateProviderRegistry(IApplicationDataSourceProvider? providerOverride = null) =>
        providerOverride is null
            ? new([
                new SqliteApplicationDataSourceProvider(), new MySqlApplicationDataSourceProvider(),
                new PostgreSqlApplicationDataSourceProvider(), new SqlServerApplicationDataSourceProvider()])
            : new([
                providerOverride, new MySqlApplicationDataSourceProvider(),
                new PostgreSqlApplicationDataSourceProvider(), new SqlServerApplicationDataSourceProvider()]);

    private static void BlockAuditInsert(ISqlSugarClient appDb) =>
        appDb.Ado.ExecuteCommand("CREATE TRIGGER block_data_studio_audit BEFORE INSERT ON app_sql_script_audits BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END");

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin", "admin", "tenant-a", "Tenant A", "MES", "Tenant A MES", "root", "system-admin",
            ["role-admin"], ["admin"], ["*"], "ALL", true, true, true, "admin"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(accessor));
    }

    private sealed class FixedWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
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

    private sealed class FixedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class NonTransactionalSqliteProvider : IApplicationDataSourceProvider
    {
        private readonly SqliteApplicationDataSourceProvider inner = new();

        public string Type => inner.Type;

        public ApplicationDataSourceProviderCapability Capability { get; } = new("Sqlite", false, false, false, true, true, true, 1000)
        {
            MaxWriteRows = 1000,
            MaxPreviewRows = 200,
            SupportsSchemas = false,
            SupportsOriginalValueConcurrency = true
        };

        public ApplicationDataSourceCatalogSql Catalog => inner.Catalog;

        public string QuoteIdentifier(string identifier) => inner.QuoteIdentifier(identifier);

        public string QuoteQualified(string? schema, string identifier) => inner.QuoteQualified(schema, identifier);

        public string BuildPageSql(string sourceSql, string orderBySql, int offset, int limit) => inner.BuildPageSql(sourceSql, orderBySql, offset, limit);

        public string BuildCountSql(string quotedTableName, string whereSql) => inner.BuildCountSql(quotedTableName, whereSql);

        public string BuildPreviewSql(string sourceSql, int maxRows) => inner.BuildPreviewSql(sourceSql, maxRows);

        public string BuildTextSearchSql(string quotedColumnName, string parameterName) => inner.BuildTextSearchSql(quotedColumnName, parameterName);

        public string BuildCreateTableSql(string? schemaName, string tableName, IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> columns) => inner.BuildCreateTableSql(schemaName, tableName, columns);

        public IReadOnlyList<string> BuildAlterTableSql(string? schemaName, string tableName, IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> currentColumns, IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> desiredColumns) => inner.BuildAlterTableSql(schemaName, tableName, currentColumns, desiredColumns);

        public string BuildCreateViewSql(string qualifiedViewName, string selectSql) => inner.BuildCreateViewSql(qualifiedViewName, selectSql);

        public string BuildCreateOrReplaceViewSql(string qualifiedViewName, string selectSql) => inner.BuildCreateOrReplaceViewSql(qualifiedViewName, selectSql);

        public string BuildDropViewSql(string qualifiedViewName) => inner.BuildDropViewSql(qualifiedViewName);

        public string BuildValidateViewSql(string qualifiedViewName) => inner.BuildValidateViewSql(qualifiedViewName);

        public string BuildParameterName(string name) => inner.BuildParameterName(name);

        public bool IsReadOnlyStatement(string sql) => inner.IsReadOnlyStatement(sql);
    }
}
