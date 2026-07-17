using System;

namespace AsterERP.Workflow.Core.Services;

public record HistoricActivityInstance
{
    public string Id { get; init; } = null!;
    public string? ActivityId { get; init; }
    public string? ActivityName { get; init; }
    public string? ActivityType { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? ExecutionId { get; init; }
    public string? TaskId { get; init; }
    public string? Assignee { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
}
