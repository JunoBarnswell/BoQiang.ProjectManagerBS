namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceCreateTableColumnRequest(
    string ColumnName,
    string DataType,
    bool Nullable,
    bool PrimaryKey,
    string? DefaultValue,
    string? Remark);
