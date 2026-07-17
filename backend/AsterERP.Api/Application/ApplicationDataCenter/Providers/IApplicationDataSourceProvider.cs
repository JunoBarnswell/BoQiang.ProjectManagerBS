using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public interface IApplicationDataSourceProvider
{
    string Type { get; }
    ApplicationDataSourceProviderCapability Capability { get; }
    ApplicationDataSourceCatalogSql Catalog { get; }
    string QuoteIdentifier(string identifier);
    string QuoteQualified(string? schema, string identifier);
    string BuildPageSql(string sourceSql, string orderBySql, int offset, int limit);
    string BuildCountSql(string quotedTableName, string whereSql);
    string BuildPreviewSql(string sourceSql, int maxRows);
    string BuildTextSearchSql(string quotedColumnName, string parameterName);
    string BuildCreateTableSql(string? schemaName, string tableName, IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> columns);
    IReadOnlyList<string> BuildAlterTableSql(
        string? schemaName,
        string tableName,
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> currentColumns,
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> desiredColumns);
    string BuildCreateViewSql(string qualifiedViewName, string selectSql);
    string BuildCreateOrReplaceViewSql(string qualifiedViewName, string selectSql);
    string BuildDropViewSql(string qualifiedViewName);
    string BuildValidateViewSql(string qualifiedViewName);
    string BuildParameterName(string name);
    bool IsReadOnlyStatement(string sql);
}
