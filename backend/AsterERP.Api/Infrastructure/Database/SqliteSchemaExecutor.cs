using System.Data;
using System.Data.Common;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Database;

public sealed class SqliteSchemaExecutor(ISqlSugarClient db)
{
    public void Execute(string sql)
    {
        db.Ado.ExecuteCommand(sql);
    }

    public void EnsureColumn(string tableName, string columnName, string definition)
    {
        if (!HasColumn(tableName, columnName))
        {
            Execute($"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {QuoteIdentifier(columnName)} {definition};");
        }
    }

    public void EnsureNullableColumn(string tableName, string columnName, string definition)
    {
        if (!HasColumn(tableName, columnName))
        {
            Execute($"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {QuoteIdentifier(columnName)} {definition};");
            return;
        }

        if (IsNullable(tableName, columnName))
        {
            return;
        }

        RebuildWithNullableColumn(tableName, columnName);
    }

    public bool HasColumn(string tableName, string columnName)
    {
        var safeTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
        var safeColumnName = columnName.Replace("'", "''", StringComparison.Ordinal);
        var result = db.Ado.GetDataTable(
            $"SELECT COUNT(1) AS ColumnCount FROM pragma_table_info('{safeTableName}') WHERE name = '{safeColumnName}'");

        return result.Rows.Count > 0 &&
               int.TryParse(result.Rows[0]["ColumnCount"]?.ToString(), out var count) &&
               count > 0;
    }

    public async Task<bool> HasColumnAsync(string tableName, string columnName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safeTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
        var safeColumnName = columnName.Replace("'", "''", StringComparison.Ordinal);
        var result = await ExecuteDataTableAsync($"SELECT COUNT(1) AS ColumnCount FROM pragma_table_info('{safeTableName}') WHERE name = '{safeColumnName}'", cancellationToken);
        return result.Rows.Count > 0 && int.TryParse(result.Rows[0]["ColumnCount"]?.ToString(), out var count) && count > 0;
    }

    public async Task<bool> HasTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safeTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
        var result = await ExecuteDataTableAsync(
            $"SELECT COUNT(1) AS TableCount FROM sqlite_master WHERE type = 'table' AND name = '{safeTableName}';",
            cancellationToken);
        return result.Rows.Count > 0 &&
               int.TryParse(result.Rows[0]["TableCount"]?.ToString(), out var count) &&
               count > 0;
    }

    public async Task<DataTable> ExecuteDataTableAsync(string sql, CancellationToken cancellationToken = default)
    {
        var connection = db.Ado.Connection as DbConnection ?? throw new InvalidOperationException("SQLite connection does not support asynchronous reads.");
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        for (var index = 0; index < reader.FieldCount; index++) table.Columns.Add(reader.GetName(index), reader.GetFieldType(index));
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = table.NewRow();
            for (var index = 0; index < reader.FieldCount; index++) row[index] = await reader.IsDBNullAsync(index, cancellationToken) ? DBNull.Value : reader.GetValue(index);
            table.Rows.Add(row);
        }
        return table;
    }

    public async Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        var connection = db.Ado.Connection as DbConnection ?? throw new InvalidOperationException("SQLite connection does not support asynchronous commands.");
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RenameColumnIfExistsAsync(
        string tableName,
        string oldColumnName,
        string newColumnName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await HasColumnAsync(tableName, oldColumnName, cancellationToken))
        {
            return;
        }

        if (await HasColumnAsync(tableName, newColumnName, cancellationToken))
        {
            var quotedTable = QuoteIdentifier(tableName);
            var quotedOld = QuoteIdentifier(oldColumnName);
            var quotedNew = QuoteIdentifier(newColumnName);
            await ExecuteNonQueryAsync(
                $"UPDATE {quotedTable} SET {quotedNew} = {quotedOld} WHERE ({quotedNew} IS NULL OR trim(CAST({quotedNew} AS TEXT)) = '') AND {quotedOld} IS NOT NULL;",
                cancellationToken);
            await ExecuteNonQueryAsync(
                $"ALTER TABLE {quotedTable} DROP COLUMN {quotedOld};",
                cancellationToken);
            return;
        }

        await ExecuteNonQueryAsync(
            $"ALTER TABLE {QuoteIdentifier(tableName)} RENAME COLUMN {QuoteIdentifier(oldColumnName)} TO {QuoteIdentifier(newColumnName)};",
            cancellationToken);
    }

    public async Task DropColumnIfExistsAsync(
        string tableName,
        string columnName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await HasColumnAsync(tableName, columnName, cancellationToken))
        {
            return;
        }

        await ExecuteNonQueryAsync(
            $"ALTER TABLE {QuoteIdentifier(tableName)} DROP COLUMN {QuoteIdentifier(columnName)};",
            cancellationToken);
    }

    public async Task DropTableIfExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await HasTableAsync(tableName, cancellationToken))
        {
            return;
        }

        await ExecuteNonQueryAsync($"DROP TABLE {QuoteIdentifier(tableName)};", cancellationToken);
    }

    private bool IsNullable(string tableName, string columnName)
    {
        var safeTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
        var safeColumnName = columnName.Replace("'", "''", StringComparison.Ordinal);
        var result = db.Ado.GetDataTable(
            $"SELECT \"notnull\" FROM pragma_table_info('{safeTableName}') WHERE name = '{safeColumnName}'");

        return result.Rows.Count > 0 && Convert.ToInt32(result.Rows[0]["notnull"]) == 0;
    }

    private void RebuildWithNullableColumn(string tableName, string columnName)
    {
        var safeTableName = tableName.Replace("'", "''", StringComparison.Ordinal);
        var tableInfo = db.Ado.GetDataTable(
            $"SELECT cid, name, type, \"notnull\", COALESCE(CAST(dflt_value AS TEXT), '') AS dflt_value, pk FROM pragma_table_info('{safeTableName}') ORDER BY cid");
        if (tableInfo.Rows.Count == 0)
        {
            throw new InvalidOperationException($"SQLite table '{tableName}' does not exist.");
        }

        var objectSql = db.Ado.GetDataTable(
            "SELECT type, sql FROM sqlite_master WHERE tbl_name = '" + tableName.Replace("'", "''", StringComparison.Ordinal) + "' AND type IN ('index', 'trigger') AND sql IS NOT NULL");
        var temporaryName = $"{tableName}__schema_migration_{Guid.NewGuid():N}";
        var columns = tableInfo.Rows.Cast<System.Data.DataRow>()
            .OrderBy(row => Convert.ToInt32(row["cid"]))
            .Select(row => BuildColumnDefinition(row, columnName))
            .ToArray();
        var primaryKeyColumns = tableInfo.Rows.Cast<System.Data.DataRow>()
            .Where(row => Convert.ToInt32(row["pk"]) > 0)
            .OrderBy(row => Convert.ToInt32(row["pk"]))
            .Select(row => QuoteIdentifier(Convert.ToString(row["name"]) ?? string.Empty))
            .ToArray();
        if (primaryKeyColumns.Length > 0)
        {
            columns = [.. columns, $"PRIMARY KEY ({string.Join(", ", primaryKeyColumns)})"];
        }

        var quotedTable = QuoteIdentifier(tableName);
        var quotedTemporary = QuoteIdentifier(temporaryName);
        var quotedColumns = string.Join(", ", tableInfo.Rows.Cast<System.Data.DataRow>()
            .OrderBy(row => Convert.ToInt32(row["cid"]))
            .Select(row => QuoteIdentifier(Convert.ToString(row["name"]) ?? string.Empty)));
        var preservedObjects = objectSql.Rows.Cast<System.Data.DataRow>()
            .Select(row => Convert.ToString(row["sql"]))
            .Where(sql => !string.IsNullOrWhiteSpace(sql))
            .Select(sql => sql!)
            .ToArray();

        db.Ado.BeginTran();
        try
        {
            Execute("PRAGMA foreign_keys = OFF;");
            Execute($"ALTER TABLE {quotedTable} RENAME TO {quotedTemporary};");
            Execute($"CREATE TABLE {quotedTable} ({string.Join(", ", columns)});");
            Execute($"INSERT INTO {quotedTable} ({quotedColumns}) SELECT {quotedColumns} FROM {quotedTemporary};");
            Execute($"DROP TABLE {quotedTemporary};");
            foreach (var sql in preservedObjects)
            {
                Execute(sql);
            }

            Execute("PRAGMA foreign_keys = ON;");
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            Execute("PRAGMA foreign_keys = ON;");
            throw;
        }
    }

    private string BuildColumnDefinition(System.Data.DataRow row, string nullableColumnName)
    {
        var name = Convert.ToString(row["name"]) ?? string.Empty;
        var type = Convert.ToString(row["type"]);
        var definition = $"{QuoteIdentifier(name)} {type}".TrimEnd();
        if (!string.Equals(name, nullableColumnName, StringComparison.OrdinalIgnoreCase) && Convert.ToInt32(row["notnull"]) != 0)
        {
            definition += " NOT NULL";
        }

        var defaultValue = Convert.ToString(row["dflt_value"]);
        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            definition += $" DEFAULT {defaultValue}";
        }

        return definition;
    }

    public void CreateIndexIfColumnsExist(string tableName, string indexName, params string[] columns)
    {
        if (columns.Length == 0 || columns.Any(column => !HasColumn(tableName, column)))
        {
            return;
        }

        var quotedTableName = QuoteIdentifier(tableName);
        var quotedIndexName = QuoteIdentifier(indexName);
        var quotedColumns = string.Join(", ", columns.Select(QuoteIdentifier));
        Execute($"CREATE INDEX IF NOT EXISTS {quotedIndexName} ON {quotedTableName}({quotedColumns});");
    }

    public static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
