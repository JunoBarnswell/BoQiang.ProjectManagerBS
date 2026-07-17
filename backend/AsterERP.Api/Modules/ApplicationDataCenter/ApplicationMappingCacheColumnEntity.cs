using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_mapping_cache_columns")]
public sealed class ApplicationMappingCacheColumnEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string CacheId { get; set; } = string.Empty;
    public string SourceResourceId { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool Nullable { get; set; }
    public int Ordinal { get; set; }
}
