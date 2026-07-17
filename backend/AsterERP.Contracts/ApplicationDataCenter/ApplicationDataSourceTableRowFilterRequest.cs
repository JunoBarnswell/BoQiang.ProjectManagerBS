namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableRowFilterRequest
{
    public string FieldCode { get; set; } = string.Empty;

    public string Operator { get; set; } = "contains";

    public object? Value { get; set; }
}
