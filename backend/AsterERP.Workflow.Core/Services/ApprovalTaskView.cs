using System;

namespace AsterERP.Workflow.Core.Services;

public sealed record ApprovalTaskView
{
    public string TaskId { get; init; } = null!;
    public string? TaskName { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? Assignee { get; init; }
    public string? Owner { get; init; }
    public string? CandidateUser { get; init; }
    public string? CandidateGroup { get; init; }
    public string? FormKey { get; init; }
    public string? BusinessKey { get; init; }
    public DateTime? CreateTime { get; init; }
    public DateTime? DueDate { get; init; }
    public string? TaskDefinitionKey { get; init; }
}
