namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceViewUpsertRequest(
    string ViewName,
    string? SchemaName,
    string Alias,
    string Sql,
    string? Remark);
