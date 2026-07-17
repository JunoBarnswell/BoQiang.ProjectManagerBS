namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterPreviewDatasetResponse(
    string Key,
    string Title,
    string SourcePath,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> Fields,
    int TotalRows,
    bool Truncated,
    string ValueKind = "array");
