using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed class PostgreSqlApplicationDataSourceProvider() : ApplicationDataSourceProviderBase(
    "PostgreSQL", "\"", "\"",
    new("PostgreSQL", true, true, true, true, true, true, 1000)
    {
        MaxWriteRows = 1000,
        MaxPreviewRows = 200,
        SupportsSchemas = true,
        SupportsOriginalValueConcurrency = true
    },
    new(
        "SELECT table_name AS TableName, table_schema AS SchemaName, table_type AS TableType FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') ORDER BY table_schema, table_name",
        "SELECT c.column_name AS ColumnName, c.data_type AS DataType, c.is_nullable = 'YES' AS Nullable, (kcu.column_name IS NOT NULL) AS PrimaryKey, c.ordinal_position AS \"OrdinalPosition\" FROM information_schema.columns c LEFT JOIN information_schema.table_constraints tc ON tc.table_schema = c.table_schema AND tc.table_name = c.table_name AND tc.constraint_type = 'PRIMARY KEY' LEFT JOIN information_schema.key_column_usage kcu ON kcu.constraint_schema = tc.constraint_schema AND kcu.constraint_name = tc.constraint_name AND kcu.column_name = c.column_name WHERE c.table_schema = COALESCE(@schema, current_schema()) AND c.table_name = @table ORDER BY c.ordinal_position",
        "SELECT constraint_type AS ConstraintType, constraint_name AS ConstraintName FROM information_schema.table_constraints WHERE table_schema = COALESCE(@schema, current_schema()) AND table_name = @table",
        "SELECT indexname AS ObjectName, 'INDEX' AS ObjectType, indexdef AS Definition FROM pg_indexes WHERE schemaname = COALESCE(@schema, current_schema()) AND tablename = @table",
        "SELECT trigger_name AS ObjectName, 'TRIGGER' AS ObjectType, action_statement AS Definition FROM information_schema.triggers WHERE event_object_schema = COALESCE(@schema, current_schema()) AND event_object_table = @table",
        "SELECT 'comment' AS ObjectName, 'COMMENT' AS ObjectType, obj_description((quote_ident(COALESCE(@schema, current_schema())) || '.' || quote_ident(@table))::regclass) AS Definition"))
{

    protected override string BuildAlterColumnSql(
        string qualifiedTableName,
        ApplicationDataSourceColumnDefinition current,
        ApplicationDataSourceColumnDefinition desired)
    {
        var clauses = new List<string>();
        if (!string.Equals(current.DataType, desired.DataType, StringComparison.OrdinalIgnoreCase))
            clauses.Add($"ALTER COLUMN {QuoteIdentifier(desired.ColumnName)} TYPE {desired.DataType.Trim().ToUpperInvariant()}");
        if (current.Nullable != desired.Nullable)
            clauses.Add($"ALTER COLUMN {QuoteIdentifier(desired.ColumnName)} {(desired.Nullable ? "DROP NOT NULL" : "SET NOT NULL")}");
        if (!string.Equals(current.DefaultSql, desired.DefaultSql, StringComparison.Ordinal))
            clauses.Add($"ALTER COLUMN {QuoteIdentifier(desired.ColumnName)} {(desired.DefaultExpression is null ? "DROP DEFAULT" : $"SET DEFAULT {desired.RenderDefault(Type)}")}");
        return $"ALTER TABLE {qualifiedTableName} {string.Join(", ", clauses)}";
    }
}
