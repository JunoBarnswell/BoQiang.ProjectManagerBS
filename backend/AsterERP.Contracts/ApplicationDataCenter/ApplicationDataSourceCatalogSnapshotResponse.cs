namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceCatalogSnapshotResponse(
    string Id,
    string DataSourceId,
    string Provider,
    string SnapshotHash,
    DateTime CapturedAt,
    IReadOnlyList<ApplicationDataSourceCatalogTableResponse> Tables)
{
    public int VersionNo { get; init; }
    public string? PreviousSnapshotId { get; init; }
    public string? PreviousSnapshotHash { get; init; }
    public IReadOnlyList<ApplicationDataSourceCatalogChangeResponse> Changes { get; init; } = [];
}

public sealed record ApplicationDataSourceCatalogChangeResponse(
    string ChangeType,
    string NodeType,
    string NodeName,
    string? SchemaName,
    string? Detail);
