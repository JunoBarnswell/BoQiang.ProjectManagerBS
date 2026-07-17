using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.QueryViews;

[SugarTable("system_query_view_definitions")]
public sealed class SystemQueryViewDefinitionEntity : EntityBase
{
    public string ViewCode { get; set; } = string.Empty;

    public string ViewName { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? MenuCode { get; set; }

    public string ViewType { get; set; } = "list";

    public bool IsDefault { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int VersionNo { get; set; }

    public int DefaultPageSize { get; set; } = 20;

    public int MaxPageSize { get; set; } = 100;

    public string Status { get; set; } = "Draft";

    public string DesignJson { get; set; } = "{}";
}
