using System;

namespace AsterERP.Workflow.Core.Services;

public record HistoricVariableInstance
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? Type { get; init; }
    public object? Value { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? TaskId { get; init; }
    public DateTime? CreateTime { get; init; }
}
