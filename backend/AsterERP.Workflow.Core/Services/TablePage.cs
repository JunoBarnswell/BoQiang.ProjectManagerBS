using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Services;

public record TablePage
{
    public List<Dictionary<string, object?>> Rows { get; init; } = new();
    public int TotalCount { get; init; }
}
