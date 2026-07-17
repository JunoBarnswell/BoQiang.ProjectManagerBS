namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPageUpsertRequest
{
    public string DesignerMode { get; set; } = "structured";
    public DateTime? ExpectedUpdatedTime { get; set; }

    public string DocumentJson { get; set; } = "{}";

    public string? ModuleId { get; set; }

    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    public string? ParentPageId { get; set; }

    public List<ApplicationDevelopmentPageParameterDto> PageParameters { get; set; } = [];

    public string PageType { get; set; } = ApplicationDevelopmentPageTypes.Standard;

    public string PermissionConfigJson { get; set; } = "{}";

    public string? Remark { get; set; }

    public int SortOrder { get; set; }

    public string TemplateCode { get; set; } = "query-list";

    public string VersionId { get; set; } = string.Empty;
}
