namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMappingCacheRefreshResponse(
    bool Success,
    string Message,
    int RowCount,
    DateTime RefreshedAt,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
