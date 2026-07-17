namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMappingCacheTestResponse(
    bool Success,
    string Message,
    long DurationMs,
    IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
