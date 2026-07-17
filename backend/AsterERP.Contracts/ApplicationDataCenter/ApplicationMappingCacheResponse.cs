namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMappingCacheResponse(
    string Id,
    string CacheKey,
    string CacheName,
    string Status,
    ApplicationMappingCacheSource Source,
    IReadOnlyList<ApplicationMappingCacheColumn> Columns,
    IReadOnlyList<ApplicationMappingCacheParameter> Parameters,
    ApplicationMappingCacheProviderCapabilityResponse Capability,
    string? Remark,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    DateTime? LastRefreshedAt,
    int? LastRowCount,
    string? LastValidationStatus,
    string? LastValidationMessage);
