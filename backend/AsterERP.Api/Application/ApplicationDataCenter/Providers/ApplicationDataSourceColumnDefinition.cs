namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed record ApplicationDataSourceColumnDefinition(
    string ColumnName,
    string DataType,
    bool Nullable,
    bool PrimaryKey,
    ApplicationDataSourceDefaultExpression? DefaultExpression,
    string? Remark)
{
    public string? DefaultSql => DefaultExpression?.Sql;

    public string RenderDefault(string providerType) =>
        DefaultExpression?.RenderFor(providerType) ?? string.Empty;
}
