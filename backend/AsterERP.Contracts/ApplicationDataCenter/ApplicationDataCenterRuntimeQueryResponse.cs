namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterRuntimeQueryResponse(
    int Total,
    int PageIndex,
    int PageSize,
    IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    string SnapshotId,
    int SnapshotVersion);
