using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Runtime;

[SugarTable("system_user_grid_views")]
public sealed class SystemUserGridViewEntity : EntityBase
{
    public string UserId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string PageCode { get; set; } = string.Empty;

    public int VersionNo { get; set; } = 1;

    [SugarColumn(Length = 262144)]
    public string ViewJson { get; set; } = "{}";
}
