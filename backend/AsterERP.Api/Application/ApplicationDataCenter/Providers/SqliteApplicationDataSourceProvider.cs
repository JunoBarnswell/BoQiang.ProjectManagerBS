using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed class SqliteApplicationDataSourceProvider() : ApplicationDataSourceProviderBase(
    "Sqlite", "\"", "\"",
    new("Sqlite", true, false, false, true, true, true, 1000)
    {
        MaxWriteRows = 1000,
        MaxPreviewRows = 200,
        SupportsSchemas = false,
        SupportsOriginalValueConcurrency = true
    },
    new(
        "SELECT name AS TableName, NULL AS SchemaName, type AS TableType FROM sqlite_master WHERE type IN ('table','view') AND name NOT LIKE 'sqlite_%' ORDER BY name",
        "SELECT name AS ColumnName, type AS DataType, CASE WHEN \"notnull\" = 0 THEN 1 ELSE 0 END AS Nullable, CASE WHEN pk > 0 THEN 1 ELSE 0 END AS PrimaryKey, cid + 1 AS OrdinalPosition FROM pragma_table_info(@table)",
        "SELECT type AS ObjectType, name AS ObjectName, sql AS Definition FROM sqlite_master WHERE tbl_name = @table AND type = 'table'",
        "SELECT 'INDEX' AS ObjectType, name AS ObjectName, sql AS Definition FROM sqlite_master WHERE tbl_name = @table AND type = 'index' AND name NOT LIKE 'sqlite_autoindex_%'",
        "SELECT 'TRIGGER' AS ObjectType, name AS ObjectName, sql AS Definition FROM sqlite_master WHERE tbl_name = @table AND type = 'trigger'",
        "SELECT 'comment' AS ObjectName, 'COMMENT' AS ObjectType, NULL AS Definition WHERE 1 = 0"))
{
    protected override string BuildAlterColumnSql(
        string qualifiedTableName,
        ApplicationDataSourceColumnDefinition current,
        ApplicationDataSourceColumnDefinition desired) =>
        throw new AsterERP.Shared.Exceptions.ValidationException(
            "SQLite 不支持在当前安全计划中修改字段类型、可空性或默认值；请通过重建表的显式迁移计划执行。",
            AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);

    public override string BuildCreateOrReplaceViewSql(string qualifiedViewName, string selectSql) =>
        throw new ValidationException(
            "SQLite 不支持原子替换视图，请使用候选视图校验和补偿流程",
            ErrorCodes.ApplicationDataCenterInvalidConfig);
}
