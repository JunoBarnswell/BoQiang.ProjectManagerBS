using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

/// <summary>Immutable runtime input produced by a successful publish.</summary>
[SugarTable("app_data_center_published_snapshots")]
public sealed class ApplicationDataCenterPublishedSnapshot : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ModuleKey { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string ObjectCode { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public int VersionNo { get; set; }
    [SugarColumn(Length = 262144)]
    public string ConfigJson { get; set; } = "{}";
    [SugarColumn(Length = 262144)]
    public string BindingJson { get; set; } = "{}";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public string PublishedBy { get; set; } = string.Empty;
}
