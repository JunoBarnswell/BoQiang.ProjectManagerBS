using System;
using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Services;

public record TaskImplementation
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? Assignee { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? Owner { get; init; }
    public int Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public string? DelegationState { get; init; }
    public string? Description { get; init; }
    public string? TaskDefinitionKey { get; init; }
    public string? ParentTaskId { get; init; }
    public string? Category { get; init; }
    public string? FormKey { get; init; }
    public DateTime? CreateTime { get; init; }
    public List<string>? CandidateUsers { get; init; }
    public List<string>? CandidateGroups { get; init; }
}
