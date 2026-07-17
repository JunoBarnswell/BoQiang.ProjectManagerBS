namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableRowsQueryRequest
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public IReadOnlyList<ApplicationDataSourceTableRowFilterRequest> Filters { get; set; } = [];

    public IReadOnlyList<ApplicationDataSourceTableRowSortRequest> Sorts { get; set; } = [];
}
