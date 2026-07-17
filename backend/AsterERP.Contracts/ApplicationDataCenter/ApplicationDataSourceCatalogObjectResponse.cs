namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceCatalogObjectResponse(
    string Name,
    string? Type,
    string? Definition);
