using System.Text;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceTableRowServiceTests : IDisposable
{
    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-table-row-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task TableRowCrud_UsesRealSqliteDataSourceAndPrimaryKey()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_people (id INTEGER PRIMARY KEY, name TEXT NOT NULL, age INTEGER NULL)");
        db.Ado.ExecuteCommand("INSERT INTO dc_people (id, name, age) VALUES (1, 'Alice', 30)");
        await InsertDataSourceAsync(db, readOnly: false);

        var service = CreateRowService(db);

        var queried = await service.QueryRowsAsync(DataSourceId, "dc_people", new ApplicationDataSourceTableRowsQueryRequest
        {
            PageIndex = 1,
            PageSize = 20,
            Keyword = "Ali"
        });

        Assert.True(queried.Editable);
        Assert.True(queried.CanInsert);
        Assert.Equal(["id"], queried.PrimaryKeys);
        Assert.Equal(1, queried.Total);
        Assert.Equal("Alice", Convert.ToString(queried.Rows[0]["name"]));

        var insertRequest = new ApplicationDataSourceTableRowUpsertRequest
        {
            Values = new Dictionary<string, object?>
            {
                ["id"] = 2,
                ["name"] = "Bob",
                ["age"] = 28
            }
        };
        var firstInsert = await service.InsertRowAsync(DataSourceId, "dc_people", insertRequest);
        var replayInsert = await service.InsertRowAsync(DataSourceId, "dc_people", insertRequest);
        Assert.True(firstInsert.Succeeded);
        Assert.Equal(firstInsert.LedgerId, replayInsert.LedgerId);
        Assert.Equal(ApplicationDataMutationLedgerStatus.Finalized, replayInsert.ExecutionStatus);
        Assert.Equal(2, Convert.ToInt32(db.Ado.GetScalar("SELECT COUNT(1) FROM dc_people")));

        await service.UpdateRowAsync(DataSourceId, "dc_people", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new Dictionary<string, object?> { ["id"] = 2 },
            OriginalValues = new Dictionary<string, object?> { ["name"] = "Bob", ["age"] = "28" },
            Values = new Dictionary<string, object?> { ["name"] = "Bobby", ["age"] = "29" },
            Confirmed = true,
            ExpectedAffectedRows = 1
        });

        var updatedName = db.Ado.GetString("SELECT name FROM dc_people WHERE id = 2");
        Assert.Equal("Bobby", updatedName);

        await service.DeleteRowAsync(DataSourceId, "dc_people", new ApplicationDataSourceTableRowDeleteRequest
        {
            KeyValues = new Dictionary<string, object?> { ["id"] = 2 },
            OriginalValues = new Dictionary<string, object?> { ["name"] = "Bobby", ["age"] = 29 },
            Confirmed = true,
            ExpectedAffectedRows = 1
        });

        var remaining = Convert.ToInt32(db.Ado.GetScalar("SELECT COUNT(1) FROM dc_people"));
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task TableRowCrud_RejectsSchemaQualifiedNameWhenProviderDoesNotSupportSchemas()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_people (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        await InsertDataSourceAsync(db, readOnly: false);

        var service = CreateRowService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.QueryRowsAsync(
            DataSourceId,
            "main.dc_people",
            new ApplicationDataSourceTableRowsQueryRequest { PageIndex = 1, PageSize = 20 }));

        Assert.Contains("does not support schema", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AiTableRowTools_ForwardConcurrencyConfirmationAndReturnMutation()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE ai_people (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO ai_people (id, name) VALUES (1, 'Alice')");
        await InsertDataSourceAsync(db, readOnly: false);

        var service = CreateRowService(db);
        var update = new DataCenterTableUpdateRowTool(service);
        var updateResult = await update.ExecuteAsync(new AiKernelFunctionContext
        {
            Arguments = new Dictionary<string, object?>
            {
                ["dataSourceId"] = DataSourceId,
                ["tableName"] = "ai_people",
                ["keyValues"] = new Dictionary<string, object?> { ["id"] = 1 },
                ["values"] = new Dictionary<string, object?> { ["name"] = "Bob" },
                ["originalValues"] = new Dictionary<string, object?> { ["name"] = "Alice" },
                ["confirmed"] = true,
                ["expectedAffectedRows"] = 1
            }
        }, CancellationToken.None);

        Assert.Contains("ledgerId", updateResult.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Bob", db.Ado.GetString("SELECT name FROM ai_people WHERE id = 1"));

        var overwrite = new DataCenterTableUpdateRowTool(service);
        await overwrite.ExecuteAsync(new AiKernelFunctionContext
        {
            Arguments = new Dictionary<string, object?>
            {
                ["dataSourceId"] = DataSourceId,
                ["tableName"] = "ai_people",
                ["keyValues"] = new Dictionary<string, object?> { ["id"] = 1 },
                ["values"] = new Dictionary<string, object?> { ["name"] = "Carol" },
                ["originalValues"] = new Dictionary<string, object?> { ["name"] = "stale" },
                ["conflictResolution"] = "overwrite",
                ["confirmed"] = true,
                ["expectedAffectedRows"] = 1
            }
        }, CancellationToken.None);

        Assert.Equal("Carol", db.Ado.GetString("SELECT name FROM ai_people WHERE id = 1"));

        var insert = new DataCenterTableInsertRowTool(service);
        await insert.ExecuteAsync(new AiKernelFunctionContext
        {
            Arguments = new Dictionary<string, object?>
            {
                ["dataSourceId"] = DataSourceId,
                ["tableName"] = "ai_people",
                ["values"] = new Dictionary<string, object?> { ["id"] = 2, ["name"] = "New" },
                ["confirmed"] = true,
                ["expectedAffectedRows"] = 1
            }
        }, CancellationToken.None);

        var delete = new DataCenterTableDeleteRowTool(service);
        var deleteResult = await delete.ExecuteAsync(new AiKernelFunctionContext
        {
            Arguments = new Dictionary<string, object?>
            {
                ["dataSourceId"] = DataSourceId,
                ["tableName"] = "ai_people",
                ["keyValues"] = new Dictionary<string, object?> { ["id"] = 2 },
                ["originalValues"] = new Dictionary<string, object?> { ["name"] = "New" },
                ["confirmed"] = true,
                ["expectedAffectedRows"] = 1
            }
        }, CancellationToken.None);

        Assert.Contains("ledgerId", deleteResult.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, Convert.ToInt32(db.Ado.GetScalar("SELECT COUNT(1) FROM ai_people")));
    }

    [Fact]
    public async Task InsertRow_WhenAuditPersistenceFails_PersistsUnknownAndDoesNotReportSuccess()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity, ApplicationSqlScriptAuditEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_people (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        await InsertDataSourceAsync(db, readOnly: false);
        db.Ado.ExecuteCommand("CREATE TRIGGER block_row_audit BEFORE INSERT ON app_sql_script_audits BEGIN SELECT RAISE(ABORT, 'audit unavailable'); END");

        var service = CreateRowService(db, withAudit: true);
        var exception = await Assert.ThrowsAsync<AggregateException>(() => service.InsertRowAsync(DataSourceId, "dc_people", new ApplicationDataSourceTableRowUpsertRequest
        {
            Values = new Dictionary<string, object?> { ["id"] = 1, ["name"] = "Alice" }
        }));

        Assert.Contains(exception.InnerExceptions, item => item.Message.Contains("audit unavailable", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, Convert.ToInt32(db.Ado.GetScalar("SELECT COUNT(1) FROM dc_people")));
        var ledger = await db.Queryable<ApplicationDataMutationLedgerEntity>().SingleAsync();
        Assert.Equal(ApplicationDataMutationLedgerStatus.Unknown, ledger.Status);
        Assert.Equal("ExternalWriteUnknown", ledger.FailureCode);
    }

    [Fact]
    public async Task QueryRows_HonorsCancellationBeforeExternalProviderRead()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_people (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO dc_people (id, name) VALUES (1, 'Alice')");
        await InsertDataSourceAsync(db, readOnly: false);
        var service = CreateRowService(db);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryRowsAsync(
            DataSourceId,
            "dc_people",
            new ApplicationDataSourceTableRowsQueryRequest(),
            cancellation.Token));
    }

    [Fact]
    public async Task UpdateRow_RejectsMissingOriginalValuesForOptimisticConcurrency()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_people (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO dc_people (id, name) VALUES (1, 'Alice')");
        await InsertDataSourceAsync(db, readOnly: false);

        var service = CreateRowService(db, withAudit: true);
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateRowAsync(DataSourceId, "dc_people", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new Dictionary<string, object?> { ["id"] = 1 },
            Values = new Dictionary<string, object?> { ["name"] = "Changed" },
            Confirmed = true,
            ExpectedAffectedRows = 1
        }));
    }

    [Fact]
    public async Task ReadOnlyDataSource_RejectsDmlBeforeAnyMutationOrAudit()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity, ApplicationSqlScriptAuditEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_read_only (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO dc_read_only (id, name) VALUES (1, 'original')");
        await InsertDataSourceAsync(db, readOnly: true);

        var service = CreateRowService(db, withAudit: true);

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateRowAsync(DataSourceId, "dc_read_only", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new Dictionary<string, object?> { ["id"] = 1 },
            OriginalValues = new Dictionary<string, object?> { ["name"] = "original" },
            Values = new Dictionary<string, object?> { ["name"] = "must-not-write" },
            Confirmed = true,
            ExpectedAffectedRows = 1
        }));

        Assert.Equal("original", db.Ado.GetString("SELECT name FROM dc_read_only WHERE id = 1"));
        Assert.Equal(0, await db.Queryable<ApplicationSqlScriptAuditEntity>().CountAsync());
    }

    [Fact]
    public async Task QueryRows_MarksTableWithoutPrimaryKeyAsNotEditable()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_logs (message TEXT NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO dc_logs (message) VALUES ('created')");
        await InsertDataSourceAsync(db, readOnly: false);

        var service = CreateRowService(db);
        var queried = await service.QueryRowsAsync(DataSourceId, "dc_logs", new ApplicationDataSourceTableRowsQueryRequest());

        Assert.False(queried.Editable);
        Assert.True(queried.CanInsert);
        Assert.Contains("没有主键", queried.EditDisabledReason);
    }

    [Fact]
    public async Task CompositePrimaryKey_ConvertsValuesAndRequiresExactConfirmation()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_composite (tenant TEXT NOT NULL, item_id INTEGER NOT NULL, quantity INTEGER NOT NULL, price DECIMAL NOT NULL, PRIMARY KEY (tenant, item_id))");
        db.Ado.ExecuteCommand("INSERT INTO dc_composite (tenant, item_id, quantity, price) VALUES ('a', 7, 2, 1.5)");
        await InsertDataSourceAsync(db, readOnly: false);
        var service = CreateRowService(db);

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateRowAsync(DataSourceId, "dc_composite", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new() { ["tenant"] = "a", ["item_id"] = "7" },
            OriginalValues = new() { ["quantity"] = "2", ["price"] = "1.5" },
            Values = new() { ["quantity"] = "3", ["price"] = "2.25" },
            Confirmed = true,
            ExpectedAffectedRows = 0
        }));

        var result = await service.UpdateRowAsync(DataSourceId, "dc_composite", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new() { ["tenant"] = "a", ["item_id"] = "7" },
            OriginalValues = new() { ["quantity"] = "2", ["price"] = "1.5" },
            Values = new() { ["quantity"] = "3", ["price"] = "2.25" },
            Confirmed = true,
            ExpectedAffectedRows = 1
        });

        Assert.True(result.Succeeded);
        Assert.Equal(3, Convert.ToInt32(db.Ado.GetScalar("SELECT quantity FROM dc_composite WHERE tenant = 'a' AND item_id = 7")));
        Assert.Equal(2.25m, Convert.ToDecimal(db.Ado.GetScalar("SELECT price FROM dc_composite WHERE tenant = 'a' AND item_id = 7")));
    }

    [Fact]
    public async Task VersionColumnWinsAndStaleWriteReturnsConflictWithServerAndLocalValues()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_versioned (id INTEGER PRIMARY KEY, version INTEGER NOT NULL, name TEXT NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO dc_versioned (id, version, name) VALUES (1, 4, 'Alice')");
        await InsertDataSourceAsync(db, readOnly: false);
        var service = CreateRowService(db, withAudit: true);

        var queried = await service.QueryRowsAsync(DataSourceId, "dc_versioned", new ApplicationDataSourceTableRowsQueryRequest());
        Assert.Equal("version", queried.ConcurrencyStrategy);
        Assert.Equal("version", queried.ConcurrencyColumn);

        var updated = await service.UpdateRowAsync(DataSourceId, "dc_versioned", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new() { ["id"] = 1 },
            VersionValue = 4,
            Values = new() { ["name"] = "Bobby" },
            Confirmed = true,
            ExpectedAffectedRows = 1
        });
        Assert.True(updated.Succeeded);
        Assert.Equal(1, Convert.ToInt32(db.Ado.GetScalar("SELECT COUNT(1) FROM app_sql_script_audits WHERE SourceKind = 'DataSourceTableRow' AND IsSuccess = 1")));
        db.Ado.ExecuteCommand("UPDATE dc_versioned SET version = 5, name = 'Bobby' WHERE id = 1");

        var conflict = await service.UpdateRowAsync(DataSourceId, "dc_versioned", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new() { ["id"] = 1 },
            VersionValue = 4,
            Values = new() { ["name"] = "Local edit" },
            Confirmed = true,
            ExpectedAffectedRows = 1
        });
        Assert.True(conflict.Conflict);
        Assert.Equal("Bobby", conflict.ServerValues["name"]);
        Assert.Equal("Local edit", conflict.LocalValues["name"]);
        Assert.Equal(1, Convert.ToInt32(db.Ado.GetScalar("SELECT COUNT(1) FROM app_sql_script_audits WHERE SourceKind = 'DataSourceTableRow' AND IsSuccess = 0")));

        var controller = new ApplicationDataSourceTablesController(null!, service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = await controller.UpdateRowAsync(DataSourceId, "dc_versioned", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new() { ["id"] = 1 },
            VersionValue = 4,
            Values = new() { ["name"] = "Second local edit" },
            Confirmed = true,
            ExpectedAffectedRows = 1
        }, CancellationToken.None);
        Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, ((ObjectResult)result).StatusCode);
    }

    [Fact]
    public async Task SqliteTimestampColumnUsesOriginalValueConcurrencyInsteadOfVersionStrategy()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_timestamped (id INTEGER PRIMARY KEY, timestamp TEXT NOT NULL, name TEXT NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO dc_timestamped (id, timestamp, name) VALUES (1, '2026-07-12T09:00:00Z', 'Alice')");
        await InsertDataSourceAsync(db, readOnly: false);
        var service = CreateRowService(db);

        var queried = await service.QueryRowsAsync(DataSourceId, "dc_timestamped", new ApplicationDataSourceTableRowsQueryRequest());

        Assert.Equal("originalValues", queried.ConcurrencyStrategy);
        Assert.Null(queried.ConcurrencyColumn);

        var updated = await service.UpdateRowAsync(DataSourceId, "dc_timestamped", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new() { ["id"] = 1 },
            OriginalValues = new() { ["timestamp"] = "2026-07-12T09:00:00Z", ["name"] = "Alice" },
            Values = new() { ["name"] = "Bobby" },
            Confirmed = true,
            ExpectedAffectedRows = 1
        });

        Assert.True(updated.Succeeded);
        Assert.Equal("Bobby", db.Ado.GetString("SELECT name FROM dc_timestamped WHERE id = 1"));
    }

    [Fact]
    public async Task CompositePrimaryKey_ConcurrentEditorsRejectStaleOriginalValues()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_concurrent (tenant TEXT NOT NULL, item_id INTEGER NOT NULL, name TEXT NOT NULL, quantity INTEGER NOT NULL, PRIMARY KEY (tenant, item_id))");
        db.Ado.ExecuteCommand("INSERT INTO dc_concurrent (tenant, item_id, name, quantity) VALUES ('tenant-a', 11, 'initial', 1)");
        await InsertDataSourceAsync(db, readOnly: false);

        var editorA = CreateRowService(db, withAudit: true);
        var editorB = CreateRowService(db, withAudit: true);
        var original = new Dictionary<string, object?> { ["name"] = "initial", ["quantity"] = 1 };

        var first = await editorA.UpdateRowAsync(DataSourceId, "dc_concurrent", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new() { ["tenant"] = "tenant-a", ["item_id"] = "11" },
            OriginalValues = original,
            Values = new() { ["name"] = "editor-a", ["quantity"] = 2 },
            Confirmed = true,
            ExpectedAffectedRows = 1
        });
        Assert.True(first.Succeeded);

        var stale = await editorB.UpdateRowAsync(DataSourceId, "dc_concurrent", new ApplicationDataSourceTableRowUpsertRequest
        {
            KeyValues = new() { ["tenant"] = "tenant-a", ["item_id"] = 11 },
            OriginalValues = original,
            Values = new() { ["name"] = "editor-b", ["quantity"] = 3 },
            Confirmed = true,
            ExpectedAffectedRows = 1
        });

        Assert.True(stale.Conflict);
        Assert.Equal("editor-a", stale.ServerValues["name"]);
        Assert.Equal("editor-b", stale.LocalValues["name"]);
        Assert.Equal("editor-a", db.Ado.GetString("SELECT name FROM dc_concurrent WHERE tenant = 'tenant-a' AND item_id = 11"));
        Assert.Equal(1, await db.Queryable<ApplicationSqlScriptAuditEntity>().Where(item => !item.IsSuccess && item.SourceKind == "DataSourceTableRow").CountAsync());
    }

    [Fact]
    public async Task StreamRowsExport_WritesCsvWithoutMaterializingExportResponse()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_export (id INTEGER PRIMARY KEY, name TEXT NOT NULL, age INTEGER NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO dc_export (id, name, age) VALUES (1, 'Alice, A', 30), (2, 'Bob, Jr', 20), (3, 'Carol', 40)");
        await InsertDataSourceAsync(db, readOnly: true);
        var service = CreateRowService(db);
        await using var output = new MemoryStream();

        var summary = await service.StreamRowsExportAsync(
            DataSourceId,
            "dc_export",
            new ApplicationDataSourceTableRowsExportRequest
            {
                Filters = [new ApplicationDataSourceTableRowFilterRequest { FieldCode = "age", Operator = "gte", Value = 30 }],
                MaxRows = 1,
                Sorts = [new ApplicationDataSourceTableRowSortRequest { FieldCode = "age", Direction = "desc" }]
            },
            output,
            CancellationToken.None);

        var csv = Encoding.UTF8.GetString(output.ToArray());
        Assert.Equal(2, summary.TotalRows);
        Assert.Equal(1, summary.ExportedRows);
        Assert.True(summary.Truncated);
        Assert.Contains("id,name,age", csv, StringComparison.Ordinal);
        Assert.Contains("3,Carol,40", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice, A", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Bob, Jr", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamRowsExport_HonorsCancellationBeforeProviderRead()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationDataSourceEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE dc_export_cancel (id INTEGER PRIMARY KEY)");
        await InsertDataSourceAsync(db, readOnly: true);
        var service = CreateRowService(db);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StreamRowsExportAsync(
            DataSourceId,
            "dc_export_cancel",
            new ApplicationDataSourceTableRowsExportRequest(),
            new MemoryStream(),
            cancellation.Token));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private const string TenantId = "tenant-a";
    private const string AppCode = "MES";
    private const string DataSourceId = "ds-sqlite-row-tests";

    private async Task InsertDataSourceAsync(ISqlSugarClient db, bool readOnly)
    {
        await db.Insertable(new ApplicationDataSourceEntity
        {
            Id = DataSourceId,
            TenantId = TenantId,
            AppCode = AppCode,
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = "row_test_sqlite",
            ObjectName = "行数据测试库",
            ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal,
            Endpoint = databasePath,
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
            {
                ["databaseName"] = databasePath,
                ["readOnly"] = readOnly
            }),
            IsReadOnly = readOnly
        }).ExecuteCommandAsync();
    }

    private ApplicationDataSourceTableRowService CreateRowService(ISqlSugarClient db, bool withAudit = false)
    {
        if (withAudit)
            db.CodeFirst.InitTables<ApplicationSqlScriptAuditEntity>();

        var currentUser = CreateCurrentUser();
        var accessor = new FixedWorkspaceDatabaseAccessor(db);
        var resolver = new ApplicationDataCenterWorkspaceResolver(currentUser);
        var connectionFactory = new ApplicationDataSourceConnectionFactory(
            new TestHostEnvironment(Path.GetDirectoryName(databasePath) ?? Path.GetTempPath()),
            new NoopApplicationDataSecretProtector(),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance));
        var providerRegistry = CreateProviderRegistry();
        var dataSourceService = new ApplicationDataSourceService(
            new WorkspaceSqlSugarRepository<ApplicationDataSourceEntity>(accessor, currentUser),
            accessor,
            resolver,
            new NoopApplicationDataSecretProtector(),
            new ApplicationDataCenterRiskGuard(),
            new ApplicationObjectReferenceService(accessor, resolver),
            new ApplicationDataCenterTemplateCatalog(),
            new ApplicationDataCenterPublishedSnapshotService(accessor, resolver),
            connectionFactory,
            new ApplicationDataPreviewReader(providerRegistry),
            providerRegistry);

        var auditWriter = withAudit
            ? new ApplicationDataCenterSqlScriptAuditWriter(accessor, resolver, currentUser, NullLogger<ApplicationDataCenterSqlScriptAuditWriter>.Instance)
            : null;
        return new ApplicationDataSourceTableRowService(accessor, resolver, connectionFactory, dataSourceService, providerRegistry, auditWriter);
    }

    private static ApplicationDataSourceProviderRegistry CreateProviderRegistry() =>
        new([
            new SqliteApplicationDataSourceProvider(),
            new MySqlApplicationDataSourceProvider(),
            new PostgreSqlApplicationDataSourceProvider(),
            new SqlServerApplicationDataSourceProvider()
        ]);

    private SqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<ApplicationDataCenterPublishedSnapshot>();
        return db;
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            TenantId,
            "客户A",
            AppCode,
            "客户A MES",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            ["*"],
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }

    private sealed class FixedWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;

        public ISqlSugarClient GetCurrentDb() => db;

        public ISqlSugarClient RequireApplicationDb() => db;

        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);

        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class NoopApplicationDataSecretProtector : IApplicationDataSecretProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;

        public string BuildPublicSecretSummary(string? cipherText) => string.IsNullOrWhiteSpace(cipherText) ? "{}" : "{\"configured\":true}";

        public string BuildPublicSecretSummary(string? cipherText, string secretRef, DateTime? updatedAt) =>
            BuildPublicSecretSummary(cipherText);
    }

    private sealed class FixedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "AsterERP.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
