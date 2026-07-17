using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_source_catalog_snapshots")]
public sealed class ApplicationDataSourceCatalogSnapshotEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;
    public int VersionNo { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? PreviousSnapshotId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? PreviousSnapshotHash { get; set; }
    public DateTime CapturedAt { get; set; }

    [SugarColumn(Length = 1048576)]
    public string CatalogJson { get; set; } = "[]";

    [SugarColumn(Length = 262144, IsNullable = true)]
    public string? ChangeJson { get; set; }
}
