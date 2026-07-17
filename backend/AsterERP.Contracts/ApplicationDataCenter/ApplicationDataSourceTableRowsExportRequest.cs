namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableRowsExportRequest
{
    public string? Keyword { get; set; }

    public IReadOnlyList<ApplicationDataSourceTableRowFilterRequest> Filters { get; set; } = [];

    public IReadOnlyList<ApplicationDataSourceTableRowSortRequest> Sorts { get; set; } = [];

    public int MaxRows { get; set; } = 10_000;
}
