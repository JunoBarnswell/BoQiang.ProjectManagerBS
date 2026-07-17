namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceCatalogRefreshRequest(string? SchemaName, string TableName);
