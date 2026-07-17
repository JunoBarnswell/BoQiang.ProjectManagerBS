namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMappingCacheTestRequest(
    IReadOnlyDictionary<string, object?>? Parameters,
    int MaxRows = 20,
    int TimeoutSeconds = 30);
