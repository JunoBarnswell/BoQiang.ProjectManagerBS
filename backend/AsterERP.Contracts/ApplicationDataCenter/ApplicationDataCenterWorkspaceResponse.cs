namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataCenterWorkspaceResponse
{
    public string? ModuleKey { get; set; }

    public string? SelectedDataSourceId { get; set; }

    public ApplicationDataCenterObjectDetailResponse? SelectedDataSource { get; set; }

    public List<ApplicationDataCenterModuleOverviewResponse> Modules { get; set; } = [];

    public List<ApplicationDataCenterTypeOptionResponse> TypeOptions { get; set; } = [];

    public List<ApplicationDataCenterTemplateResponse> Templates { get; set; } = [];

    public List<ApplicationDataCenterObjectListItemResponse> DataSources { get; set; } = [];

    public List<ApplicationDataCenterObjectListItemResponse> RecentItems { get; set; } = [];
}
