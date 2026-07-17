namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceColumnResponse(
    string ColumnName,
    string DataType,
    bool Nullable,
    bool PrimaryKey,
    int Order)
{
    public string ResourceId { get; init; } = string.Empty;
}
