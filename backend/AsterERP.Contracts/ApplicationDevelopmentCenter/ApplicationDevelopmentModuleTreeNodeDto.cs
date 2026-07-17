namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentModuleTreeNodeDto
{
    public List<ApplicationDevelopmentModuleTreeNodeDto> Children { get; set; } = [];

    public string Id { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public string? ParentModuleId { get; set; }

    public int PageCount { get; set; }

    public int SortOrder { get; set; }

    public string VersionId { get; set; } = string.Empty;
}
