namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableRowSortRequest
{
    public string FieldCode { get; set; } = string.Empty;

    public string Direction { get; set; } = "asc";
}
