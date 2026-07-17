namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMappingCacheProviderCapabilityResponse(
    string Provider,
    bool SupportsStructuredMappingCache,
    bool SupportsParameters,
    int MaxColumns,
    int MaxParameters,
    int MaxRows,
    ApplicationDataSourceSupportLevel SupportLevel = ApplicationDataSourceSupportLevel.Full,
    string? SupportReason = null);
