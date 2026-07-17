namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMappingCacheUpsertRequest(
    string CacheKey,
    string CacheName,
    ApplicationMappingCacheSource Source,
    IReadOnlyList<ApplicationMappingCacheColumn> Columns,
    IReadOnlyList<ApplicationMappingCacheParameter> Parameters,
    string? Remark);
