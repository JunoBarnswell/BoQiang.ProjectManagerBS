namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPageCreateRequest
{
    public string? ModuleId { get; set; }

    public string? PageCode { get; set; }

    public string PageName { get; set; } = string.Empty;

    public string? ParentPageId { get; set; }

    public List<ApplicationDevelopmentPageParameterDto> PageParameters { get; set; } = [];

    public string PageType { get; set; } = ApplicationDevelopmentPageTypes.Standard;

    public int SortOrder { get; set; }

    public string VersionId { get; set; } = string.Empty;
}
