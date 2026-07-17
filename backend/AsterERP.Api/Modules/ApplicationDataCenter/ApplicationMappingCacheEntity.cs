using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_mapping_caches")]
public sealed class ApplicationMappingCacheEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string CacheKey { get; set; } = string.Empty;
    public string CacheName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? SchemaName { get; set; }
    public string SourceResourceId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? ObjectName { get; set; }
    public string Status { get; set; } = "Draft";
    public int VersionNo { get; set; } = 1;
    [SugarColumn(IsNullable = true)] public DateTime? LastRefreshedAt { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? LastValidatedAt { get; set; }
    [SugarColumn(IsNullable = true)] public int? LastRowCount { get; set; }
    [SugarColumn(IsNullable = true)] public string? LastValidationStatus { get; set; }
    [SugarColumn(Length = 2000, IsNullable = true)] public string? LastValidationMessage { get; set; }
}
