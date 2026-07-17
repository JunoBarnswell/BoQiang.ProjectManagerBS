namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceCatalogColumnResponse(
    string ColumnName,
    string DataType,
    bool Nullable,
    bool PrimaryKey,
    int Order,
    string? ConcurrencyKind = null,
    string? Comment = null)
{
    public string ResourceId { get; init; } = string.Empty;
}
