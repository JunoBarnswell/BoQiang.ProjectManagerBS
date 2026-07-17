using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Printing;

[SugarTable("system_print_templates")]
public sealed class SystemPrintTemplateEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    public string Scene { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public bool IsDefault { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? DraftDataJson { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? DraftExtJson { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? DraftPermissionsJson { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? PublishedDataJson { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? PublishedExtJson { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? PublishedPermissionsJson { get; set; }
}
