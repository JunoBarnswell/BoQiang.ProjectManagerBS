namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceCatalogTableResponse(
    string TableName,
    string? SchemaName,
    string TableType,
    IReadOnlyList<ApplicationDataSourceCatalogColumnResponse> Columns,
    IReadOnlyList<ApplicationDataSourceCatalogObjectResponse> Constraints,
    IReadOnlyList<ApplicationDataSourceCatalogObjectResponse> Indexes,
    IReadOnlyList<ApplicationDataSourceCatalogObjectResponse> Triggers,
    IReadOnlyList<ApplicationDataSourceCatalogObjectResponse>? Comments = null)
{
    public string ResourceId { get; init; } = string.Empty;
}
