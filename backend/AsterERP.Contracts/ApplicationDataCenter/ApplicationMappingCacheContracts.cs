namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMappingCacheSource(
    string DataSourceId,
    string ResourceId,
    string? SchemaName,
    string Provider);

public sealed record ApplicationMappingCacheTarget(
    string CacheKey,
    string CacheName);

public sealed record ApplicationMappingCacheColumn(
    string SourceResourceId,
    string TargetName,
    string DataType,
    bool Nullable,
    int Ordinal);

public sealed record ApplicationMappingCacheParameter(
    string ResourceId,
    string Name,
    string ColumnResourceId,
    string DataType,
    bool Required,
    object? DefaultValue);
