namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentWorkspaceResponse
{
    public ApplicationDevelopmentOverviewResponse Overview { get; set; } = new();

    public string? SelectedVersionId { get; set; }

    public ApplicationDevelopmentVersionDto? SelectedVersion { get; set; }

    public List<ApplicationDevelopmentVersionDto> Versions { get; set; } = [];

    public List<ApplicationDevelopmentModuleTreeNodeDto> Modules { get; set; } = [];

    public List<ApplicationDevelopmentPageListItemDto> Pages { get; set; } = [];

    public List<ApplicationDevelopmentSharedResourceListItemDto> SharedResources { get; set; } = [];

    public List<ApplicationDevelopmentPageListItemDto> RecentPages { get; set; } = [];
}
