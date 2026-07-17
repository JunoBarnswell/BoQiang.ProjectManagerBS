using System;

namespace AsterERP.Workflow.Core.Services;

public record HistoricData
{
    public string Id { get; init; } = null!;
    public string? Type { get; init; }
    public DateTime? Timestamp { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ActivityId { get; init; }
    public string? ActivityName { get; init; }
    public string? ActivityType { get; init; }
    public string? TaskId { get; init; }
    public string? TaskName { get; init; }
    public string? VariableName { get; init; }
    public object? VariableValue { get; init; }
}
