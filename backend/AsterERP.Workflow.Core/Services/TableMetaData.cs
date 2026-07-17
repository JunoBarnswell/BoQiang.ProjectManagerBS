using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Services;

public record TableMetaData
{
    public string Name { get; init; } = null!;
    public List<string> ColumnNames { get; init; } = new();
    public List<string> ColumnTypes { get; init; } = new();
}
