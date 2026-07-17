using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Menus;

[SugarTable("system_menus")]
public sealed class SystemMenuEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string MenuName { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ParentCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RoutePath { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ComponentName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PageCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ArtifactId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ScopeType { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ConfigJson { get; set; }

    public string MenuType { get; set; } = "Menu";

    public int SortOrder { get; set; }

    public bool Visible { get; set; } = true;

    [SugarColumn(IsNullable = true)]
    public string? PermissionCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Icon { get; set; }
}
