namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPageListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string? ModuleId { get; set; }

    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    public string? ParentPageId { get; set; }

    public List<ApplicationDevelopmentPageParameterDto> PageParameters { get; set; } = [];

    public string? PageParametersJson { get; set; }

    public string PageType { get; set; } = ApplicationDevelopmentPageTypes.Standard;

    public string? PreviewMenuCode { get; set; }

    public string? PreviewRoutePath { get; set; }

    public string? PublishedMenuCode { get; set; }

    public string? PublishedArtifactId { get; set; }

    public string? PublishedRoutePath { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }

    public string VersionId { get; set; } = string.Empty;
}
