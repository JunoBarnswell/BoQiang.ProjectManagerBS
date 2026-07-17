using System;

namespace AsterERP.Workflow.Core.Services;

public record HistoricTaskInstance
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? Assignee { get; init; }
    public string? Owner { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public string? DeleteReason { get; init; }
    public string? TaskDefinitionKey { get; init; }
}
