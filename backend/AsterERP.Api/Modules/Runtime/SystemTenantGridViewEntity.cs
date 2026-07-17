using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Runtime;

[SugarTable("system_tenant_grid_views")]
public sealed class SystemTenantGridViewEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string PageCode { get; set; } = string.Empty;

    public int VersionNo { get; set; } = 1;

    [SugarColumn(Length = 262144)]
    public string ViewJson { get; set; } = "{}";
}
