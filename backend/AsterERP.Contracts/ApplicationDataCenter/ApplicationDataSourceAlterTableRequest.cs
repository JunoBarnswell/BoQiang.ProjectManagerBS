namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceAlterTableRequest(
    string TableName,
    string? SchemaName,
    IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> Columns);
