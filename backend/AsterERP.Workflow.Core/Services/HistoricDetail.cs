using System;

namespace AsterERP.Workflow.Core.Services;

public record HistoricDetail
{
    public string Id { get; init; } = null!;
    public string? Type { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ActivityId { get; init; }
    public string? VariableName { get; init; }
    public object? VariableValue { get; init; }
    public DateTime? Time { get; init; }
    public string? TaskId { get; init; }
}
