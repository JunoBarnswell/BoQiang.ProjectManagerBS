namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataCenterRuntimeQueryRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public IReadOnlyList<ApplicationDataCenterRuntimeQueryFilterRequest> Filters { get; set; } = [];
    public IReadOnlyList<ApplicationDataCenterRuntimeQuerySortRequest> Sorts { get; set; } = [];
}
