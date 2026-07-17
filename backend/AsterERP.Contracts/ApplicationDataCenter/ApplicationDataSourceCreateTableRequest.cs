namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceCreateTableRequest(
    string TableName,
    string? SchemaName,
    string? Alias,
    string? Remark,
    IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> Columns);
