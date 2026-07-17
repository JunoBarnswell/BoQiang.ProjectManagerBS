using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Organizations;

[SugarTable("system_positions")]
public sealed class SystemPositionEntity : EntityBase
{
    public string PositionCode { get; set; } = string.Empty;

    public string PositionName { get; set; } = string.Empty;

    public string DeptId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? PositionLevel { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = "Enabled";
}
