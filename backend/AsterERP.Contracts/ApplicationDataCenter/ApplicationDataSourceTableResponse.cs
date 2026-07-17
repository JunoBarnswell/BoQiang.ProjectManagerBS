namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceTableResponse(
    string TableName,
    string? SchemaName,
    string TableType)
{
    public string ResourceId { get; init; } = string.Empty;
}
