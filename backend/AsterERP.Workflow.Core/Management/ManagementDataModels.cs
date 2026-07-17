namespace AsterERP.Workflow.Core.Management;

public record TableMetaData
{
    public string Name { get; init; } = null!;
    public long RowCount { get; init; }
    public List<TableColumnMetaData> Columns { get; init; } = new();
}

public record TableColumnMetaData
{
    public string Name { get; init; } = null!;
    public string Type { get; init; } = null!;
    public bool IsNullable { get; init; }
    public bool IsPrimaryKey { get; init; }
}

public record TablePage
{
    public string TableName { get; init; } = null!;
    public int FirstResult { get; init; }
    public int MaxResults { get; init; }
    public long TotalCount { get; init; }
    public List<Dictionary<string, object?>> Rows { get; init; } = new();
}
