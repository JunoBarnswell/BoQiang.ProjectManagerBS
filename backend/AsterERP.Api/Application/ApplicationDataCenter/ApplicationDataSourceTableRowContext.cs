using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

internal sealed class ApplicationDataSourceTableRowContext(
    ApplicationDataSourceEntity dataSource,
    ApplicationDataSourceTableResponse table,
    IReadOnlyList<ApplicationDataSourceColumnResponse> columns,
    string quotedTableName)
{
    public ApplicationDataSourceEntity DataSource { get; } = dataSource;

    public ApplicationDataSourceTableResponse Table { get; } = table;

    public IReadOnlyList<ApplicationDataSourceColumnResponse> Columns { get; } = columns;

    public string QuotedTableName { get; } = quotedTableName;

    public HashSet<string> PrimaryKeys { get; } = columns
        .Where(column => column.PrimaryKey)
        .Select(column => column.ColumnName)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public ApplicationDataSourceColumnResponse RequireColumn(string fieldCode) =>
        Columns.FirstOrDefault(column => string.Equals(column.ColumnName, fieldCode.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new ValidationException($"字段不存在：{fieldCode}", ErrorCodes.ApplicationDataCenterInvalidConfig);
}
