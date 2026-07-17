namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceSqlPreviewRequest(
    string Sql,
    int MaxRows = 20);
