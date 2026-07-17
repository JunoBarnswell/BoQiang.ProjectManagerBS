using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_mapping_cache_parameters")]
public sealed class ApplicationMappingCacheParameterEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string CacheId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ColumnResourceId { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool Required { get; set; }
    [SugarColumn(Length = 4000, IsNullable = true)] public string? DefaultValueJson { get; set; }
}
