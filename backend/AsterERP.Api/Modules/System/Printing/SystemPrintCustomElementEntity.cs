using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Printing;

[SugarTable("system_print_custom_elements")]
public sealed class SystemPrintCustomElementEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? ElementJson { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? ExtJson { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? PermissionsJson { get; set; }
}
