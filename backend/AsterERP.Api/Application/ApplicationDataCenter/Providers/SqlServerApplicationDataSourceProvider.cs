using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed class SqlServerApplicationDataSourceProvider() : ApplicationDataSourceProviderBase(
    "SqlServer", "[", "]",
    new("SqlServer", true, true, true, false, true, true, 1000)
    {
        MaxWriteRows = 1000,
        MaxPreviewRows = 200,
        SupportsSchemas = true,
        SupportsOriginalValueConcurrency = true
    },
    new(
        "SELECT TABLE_NAME AS TableName, TABLE_SCHEMA AS SchemaName, TABLE_TYPE AS TableType FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_SCHEMA, TABLE_NAME",
        "SELECT c.name AS ColumnName, t.name AS DataType, CONVERT(bit, c.is_nullable) AS Nullable, CONVERT(bit, CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END) AS PrimaryKey, c.column_id AS OrdinalPosition FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id JOIN sys.tables tb ON c.object_id = tb.object_id LEFT JOIN (SELECT ic.object_id, ic.column_id FROM sys.index_columns ic JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id WHERE i.is_primary_key = 1) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id WHERE SCHEMA_NAME(tb.schema_id) = COALESCE(@schema, SCHEMA_NAME(tb.schema_id)) AND tb.name = @table ORDER BY c.column_id",
        "SELECT tc.CONSTRAINT_TYPE AS ConstraintType, tc.CONSTRAINT_NAME AS ConstraintName FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc WHERE tc.TABLE_SCHEMA = COALESCE(@schema, tc.TABLE_SCHEMA) AND tc.TABLE_NAME = @table",
        "SELECT i.name AS ObjectName, i.type_desc AS ObjectType, NULL AS Definition FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE t.name = @table",
        "SELECT tr.name AS ObjectName, 'TRIGGER' AS ObjectType, sm.definition AS Definition FROM sys.triggers tr JOIN sys.sql_modules sm ON sm.object_id = tr.object_id WHERE tr.parent_id = OBJECT_ID(COALESCE(@schema + '.', '') + @table)",
        "SELECT CAST(ep.name AS nvarchar(255)) AS ObjectName, 'COMMENT' AS ObjectType, CAST(ep.value AS nvarchar(max)) AS Definition FROM sys.extended_properties ep JOIN sys.tables tb ON tb.object_id = ep.major_id WHERE tb.name = @table AND ep.minor_id = 0"))
{
    protected override string BuildAlterColumnSql(
        string qualifiedTableName,
        ApplicationDataSourceColumnDefinition current,
        ApplicationDataSourceColumnDefinition desired)
    {
        if (!string.Equals(current.DefaultSql, desired.DefaultSql, StringComparison.Ordinal))
            throw new AsterERP.Shared.Exceptions.ValidationException("SQL Server 字段默认值变更必须通过显式约束计划。", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);

        var nullability = desired.Nullable ? "NULL" : "NOT NULL";
        return $"ALTER TABLE {qualifiedTableName} ALTER COLUMN {QuoteIdentifier(desired.ColumnName)} {NormalizeDataTypeForAlter(desired.DataType)} {nullability}";
    }

    private static string NormalizeDataTypeForAlter(string dataType) => dataType.Trim().ToUpperInvariant();

    public override string BuildPageSql(string sourceSql, string orderBySql, int offset, int limit)
    {
        ValidatePage(offset, limit);
        return $"{NormalizeSourceSql(sourceSql)}{orderBySql} OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";
    }

    public override string BuildPreviewSql(string sourceSql, int maxRows) =>
        $"SELECT TOP {ValidatePreview(maxRows)} * FROM ({NormalizeSourceSql(sourceSql)}) AS preview_source";

    public override string BuildTextSearchSql(string quotedColumnName, string parameterName) =>
        $"CAST({quotedColumnName} AS NVARCHAR(MAX)) LIKE {BuildParameterName(parameterName.TrimStart('@'))}";

    public override string BuildDropViewSql(string qualifiedViewName) =>
        $"IF OBJECT_ID(N'{RequireSqlFragment(qualifiedViewName).Replace("'", "''", StringComparison.Ordinal)}', N'V') IS NOT NULL DROP VIEW {RequireSqlFragment(qualifiedViewName)}";

    public override string BuildCreateOrReplaceViewSql(string qualifiedViewName, string selectSql) =>
        $"CREATE OR ALTER VIEW {RequireSqlFragment(qualifiedViewName)} AS {NormalizeSourceSql(selectSql)}";

    private static int ValidatePreview(int maxRows)
    {
        if (maxRows is < 1 or > 200)
            throw new AsterERP.Shared.Exceptions.ValidationException("预览行数超过数据源能力上限", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        return maxRows;
    }
}
