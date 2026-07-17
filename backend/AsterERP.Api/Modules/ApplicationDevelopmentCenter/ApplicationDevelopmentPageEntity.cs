using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_dev_pages")]
public sealed class ApplicationDevelopmentPageEntity : EntityBase
{
    public string AppCode { get; set; } = string.Empty;

    public string DesignerMode { get; set; } = "structured";

    [SugarColumn(IsNullable = true)]
    public string? ModuleId { get; set; }

    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ParentPageId { get; set; }

    public string PageParametersJson { get; set; } = "[]";

    public string PageType { get; set; } = "standard";

    public string PermissionConfigJson { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public string? PreviewMenuCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PublishedMenuCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PublishedMenuId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PublishedArtifactId { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = "Draft";

    public string TemplateCode { get; set; } = "query-list";

    public string TenantId { get; set; } = string.Empty;

    public string VersionId { get; set; } = string.Empty;
}
