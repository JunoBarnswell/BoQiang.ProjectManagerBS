using System;

namespace AsterERP.Workflow.Core.Services;

public sealed record ApprovalProcessView
{
    public string ProcessInstanceId { get; init; } = null!;
    public string? ProcessDefinitionId { get; init; }
    public string? BusinessKey { get; init; }
    public string? StartUserId { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public bool IsCompleted { get; init; }
}
