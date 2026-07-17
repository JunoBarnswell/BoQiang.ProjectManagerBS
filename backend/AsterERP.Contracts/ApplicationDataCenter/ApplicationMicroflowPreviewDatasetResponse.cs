namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowPreviewDatasetResponse(
    string Key,
    string Title,
    string SourcePath,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> Fields,
    int TotalRows,
    bool Truncated);
