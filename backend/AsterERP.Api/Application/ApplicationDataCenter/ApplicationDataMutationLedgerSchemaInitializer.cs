using System.Data.Common;
using AsterERP.Api.Modules.ApplicationDataCenter;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataMutationLedgerSchemaInitializer
{
    private const string TableName = "app_data_mutation_ledgers";
    private const string RequestIndexName = "ux_app_data_mutation_ledger_request";
    private const string StatusIndexName = "idx_app_data_mutation_ledger_status";
    private const string LeaseIndexName = "idx_app_data_mutation_ledger_lease";

    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    public async Task EnsureAsync(
        ISqlSugarClient db,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SchemaGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.CodeFirst.InitTables<ApplicationDataMutationLedgerEntity>();
            var dbType = db.CurrentConnectionConfig.DbType;
            await EnsureColumnAsync(db, "LeaseExpiresAt", BuildColumnType(dbType, "LeaseExpiresAt"), cancellationToken);
            await EnsureColumnAsync(db, "LeaseToken", BuildColumnType(dbType, "LeaseToken"), cancellationToken);
            await EnsureColumnAsync(db, "StatusHistoryJson", BuildColumnType(dbType, "StatusHistoryJson"), cancellationToken);
            await EnsureIndexAsync(db, RequestIndexName, BuildRequestIndexSql(dbType), cancellationToken);
            await EnsureIndexAsync(db, StatusIndexName, BuildStatusIndexSql(dbType), cancellationToken);
            await EnsureIndexAsync(db, LeaseIndexName, BuildLeaseIndexSql(dbType), cancellationToken);
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    private static async Task EnsureColumnAsync(
        ISqlSugarClient db,
        string columnName,
        string columnType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dbType = db.CurrentConnectionConfig.DbType;
        if (await ExecuteIntAsync(db, BuildColumnExistsSql(dbType, columnName), cancellationToken) > 0)
        {
            return;
        }

        await db.Ado.ExecuteCommandAsync(
            BuildAddColumnSql(dbType, columnName, columnType),
            Array.Empty<SugarParameter>(),
            cancellationToken);
    }

    private static async Task EnsureIndexAsync(
        ISqlSugarClient db,
        string indexName,
        string createSql,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dbType = db.CurrentConnectionConfig.DbType;
        if (await ExecuteIntAsync(db, BuildIndexExistsSql(dbType, indexName), cancellationToken) > 0)
        {
            return;
        }

        await db.Ado.ExecuteCommandAsync(createSql, Array.Empty<SugarParameter>(), cancellationToken);
    }

    private static string BuildColumnExistsSql(DbType dbType, string columnName) => dbType switch
    {
        DbType.Sqlite => $"SELECT COUNT(1) FROM pragma_table_info('{EscapeLiteral(TableName)}') WHERE name = '{EscapeLiteral(columnName)}'",
        DbType.MySql => $"SELECT COUNT(1) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '{EscapeLiteral(TableName)}' AND column_name = '{EscapeLiteral(columnName)}'",
        DbType.PostgreSQL => $"SELECT COUNT(1) FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{EscapeLiteral(TableName)}' AND column_name = '{EscapeLiteral(columnName)}'",
        DbType.SqlServer => $"SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID(N'{EscapeLiteral(TableName)}') AND name = N'{EscapeLiteral(columnName)}'",
        _ => throw new InvalidOperationException($"Unsupported application database provider '{dbType}'.")
    };

    private static string BuildIndexExistsSql(DbType dbType, string indexName) => dbType switch
    {
        DbType.Sqlite => $"SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = '{EscapeLiteral(indexName)}'",
        DbType.MySql => $"SELECT COUNT(1) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = '{EscapeLiteral(TableName)}' AND index_name = '{EscapeLiteral(indexName)}'",
        DbType.PostgreSQL => $"SELECT COUNT(1) FROM pg_indexes WHERE schemaname = current_schema() AND tablename = '{EscapeLiteral(TableName)}' AND indexname = '{EscapeLiteral(indexName)}'",
        DbType.SqlServer => $"SELECT COUNT(1) FROM sys.indexes WHERE object_id = OBJECT_ID(N'{EscapeLiteral(TableName)}') AND name = N'{EscapeLiteral(indexName)}'",
        _ => throw new InvalidOperationException($"Unsupported application database provider '{dbType}'.")
    };

    private static string BuildAddColumnSql(DbType dbType, string columnName, string columnType) => dbType switch
    {
        DbType.Sqlite => $"ALTER TABLE {TableName} ADD COLUMN {columnName} {columnType};",
        DbType.MySql => $"ALTER TABLE `{TableName}` ADD COLUMN `{columnName}` {columnType};",
        DbType.PostgreSQL => $"ALTER TABLE \"{TableName}\" ADD COLUMN \"{columnName}\" {columnType};",
        DbType.SqlServer => $"ALTER TABLE [{TableName}] ADD [{columnName}] {columnType};",
        _ => throw new InvalidOperationException($"Unsupported application database provider '{dbType}'.")
    };

    private static string BuildRequestIndexSql(DbType dbType) => dbType switch
    {
        DbType.Sqlite => $"CREATE UNIQUE INDEX IF NOT EXISTS {RequestIndexName} ON {TableName}(TenantId, AppCode, ActorUserId, Operation, RequestHash) WHERE IsDeleted = 0;",
        DbType.MySql => $"CREATE UNIQUE INDEX `{RequestIndexName}` ON `{TableName}`(TenantId, AppCode, ActorUserId, Operation, RequestHash, IsDeleted);",
        DbType.PostgreSQL => $"CREATE UNIQUE INDEX IF NOT EXISTS {RequestIndexName} ON {TableName}(TenantId, AppCode, ActorUserId, Operation, RequestHash) WHERE IsDeleted = false;",
        DbType.SqlServer => $"CREATE UNIQUE INDEX [{RequestIndexName}] ON [{TableName}](TenantId, AppCode, ActorUserId, Operation, RequestHash) WHERE IsDeleted = 0;",
        _ => throw new InvalidOperationException($"Unsupported application database provider '{dbType}'.")
    };

    private static string BuildStatusIndexSql(DbType dbType) => dbType switch
    {
        DbType.Sqlite => $"CREATE INDEX IF NOT EXISTS {StatusIndexName} ON {TableName}(TenantId, AppCode, Status, ReservedAt);",
        DbType.MySql => $"CREATE INDEX `{StatusIndexName}` ON `{TableName}`(TenantId, AppCode, Status, ReservedAt);",
        DbType.PostgreSQL => $"CREATE INDEX IF NOT EXISTS {StatusIndexName} ON {TableName}(TenantId, AppCode, Status, ReservedAt);",
        DbType.SqlServer => $"CREATE INDEX [{StatusIndexName}] ON [{TableName}](TenantId, AppCode, Status, ReservedAt);",
        _ => throw new InvalidOperationException($"Unsupported application database provider '{dbType}'.")
    };

    private static string BuildLeaseIndexSql(DbType dbType) => dbType switch
    {
        DbType.Sqlite => $"CREATE INDEX IF NOT EXISTS {LeaseIndexName} ON {TableName}(TenantId, AppCode, Status, LeaseExpiresAt);",
        DbType.MySql => $"CREATE INDEX `{LeaseIndexName}` ON `{TableName}`(TenantId, AppCode, Status, LeaseExpiresAt);",
        DbType.PostgreSQL => $"CREATE INDEX IF NOT EXISTS {LeaseIndexName} ON {TableName}(TenantId, AppCode, Status, LeaseExpiresAt);",
        DbType.SqlServer => $"CREATE INDEX [{LeaseIndexName}] ON [{TableName}](TenantId, AppCode, Status, LeaseExpiresAt);",
        _ => throw new InvalidOperationException($"Unsupported application database provider '{dbType}'.")
    };

    private static string BuildColumnType(DbType dbType, string columnName) => dbType switch
    {
        DbType.Sqlite => "TEXT NULL",
        DbType.MySql => columnName == "LeaseExpiresAt" ? "DATETIME(6) NULL" : columnName == "LeaseToken" ? "VARCHAR(64) NULL" : "TEXT NULL",
        DbType.PostgreSQL => columnName == "LeaseExpiresAt" ? "TIMESTAMP NULL" : columnName == "LeaseToken" ? "VARCHAR(64) NULL" : "TEXT NULL",
        DbType.SqlServer => columnName == "LeaseExpiresAt" ? "datetime2 NULL" : columnName == "LeaseToken" ? "nvarchar(64) NULL" : "nvarchar(max) NULL",
        _ => throw new InvalidOperationException($"Unsupported application database provider '{dbType}'.")
    };

    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static async Task<int> ExecuteIntAsync(
        ISqlSugarClient db,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("The database connection does not support asynchronous scalar queries.");
        if (connection.State != global::System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, global::System.Globalization.CultureInfo.InvariantCulture);
    }
}
