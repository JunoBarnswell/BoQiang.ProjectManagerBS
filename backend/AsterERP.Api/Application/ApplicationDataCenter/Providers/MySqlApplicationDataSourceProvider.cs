using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed class MySqlApplicationDataSourceProvider() : ApplicationDataSourceProviderBase(
    "MySql", "`", "`",
    new("MySql", false, true, false, false, true, true, 1000)
    {
        MaxWriteRows = 1000,
        MaxPreviewRows = 200,
        SupportsSchemas = true,
        SupportsOriginalValueConcurrency = true
    },
    new(
        "SELECT TABLE_NAME AS TableName, TABLE_SCHEMA AS SchemaName, TABLE_TYPE AS TableType FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_SCHEMA, TABLE_NAME",
        "SELECT COLUMN_NAME AS ColumnName, COLUMN_TYPE AS DataType, IS_NULLABLE = 'YES' AS Nullable, COLUMN_KEY = 'PRI' AS PrimaryKey, ORDINAL_POSITION AS OrdinalPosition FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = COALESCE(@schema, DATABASE()) AND TABLE_NAME = @table ORDER BY ORDINAL_POSITION",
        "SELECT CONSTRAINT_TYPE AS ConstraintType, CONSTRAINT_NAME AS ConstraintName FROM information_schema.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table",
        "SELECT INDEX_NAME AS ObjectName, INDEX_TYPE AS ObjectType, NULL AS Definition FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = COALESCE(@schema, DATABASE()) AND TABLE_NAME = @table",
        "SELECT TRIGGER_NAME AS ObjectName, 'TRIGGER' AS ObjectType, ACTION_STATEMENT AS Definition FROM information_schema.TRIGGERS WHERE EVENT_OBJECT_SCHEMA = COALESCE(@schema, DATABASE()) AND EVENT_OBJECT_TABLE = @table",
        "SELECT 'comment' AS ObjectName, 'COMMENT' AS ObjectType, TABLE_COMMENT AS Definition FROM information_schema.TABLES WHERE TABLE_SCHEMA = COALESCE(@schema, DATABASE()) AND TABLE_NAME = @table"))
{
    protected override string BuildAlterColumnSql(
        string qualifiedTableName,
        ApplicationDataSourceColumnDefinition current,
        ApplicationDataSourceColumnDefinition desired) =>
        $"ALTER TABLE {qualifiedTableName} MODIFY COLUMN {BuildMySqlColumnDefinition(desired)}";

    private string BuildMySqlColumnDefinition(ApplicationDataSourceColumnDefinition column)
    {
        var definition = $"{QuoteIdentifier(column.ColumnName)} {column.DataType.Trim().ToUpperInvariant()}";
        if (!column.Nullable || column.PrimaryKey)
            definition += " NOT NULL";
        if (column.DefaultExpression is not null)
            definition += $" DEFAULT {column.RenderDefault(Type)}";
        return definition;
    }

    public override string BuildTextSearchSql(string quotedColumnName, string parameterName) =>
        $"CAST({quotedColumnName} AS CHAR) LIKE {BuildParameterName(parameterName.TrimStart('@'))}";
}
