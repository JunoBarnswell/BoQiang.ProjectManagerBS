namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPageDetailDto
{
    public string DesignerMode { get; set; } = string.Empty;
    public DateTime? UpdatedTime { get; set; }

    public string Id { get; set; } = string.Empty;

    public string DocumentJson { get; set; } = "{}";

    public string? ModuleId { get; set; }

    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    public string? ParentPageId { get; set; }

    public List<ApplicationDevelopmentPageParameterDto> PageParameters { get; set; } = [];

    public string? PageParametersJson { get; set; }

    public string PageType { get; set; } = ApplicationDevelopmentPageTypes.Standard;

    public string PermissionConfigJson { get; set; } = "{}";

    public string? PreviewMenuCode { get; set; }

    public string? PreviewRoutePath { get; set; }

    public string? PublishedMenuCode { get; set; }

    public string? PublishedMenuId { get; set; }

    public string? PublishedArtifactId { get; set; }

    public string? PublishedArtifactJson { get; set; }

    public string? PublishedArtifactHash { get; set; }

    public int? PublishedArtifactRevision { get; set; }

    public string? PublishedManifestHash { get; set; }

    public DateTime? PublishedSchemaUpdatedTime { get; set; }

    public string? PublishedRoutePath { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = string.Empty;

    public string VersionId { get; set; } = string.Empty;
}
