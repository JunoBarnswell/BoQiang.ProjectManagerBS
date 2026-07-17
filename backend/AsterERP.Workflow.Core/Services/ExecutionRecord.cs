namespace AsterERP.Workflow.Core.Services;

public record ExecutionRecord
{
    public string Id { get; init; } = null!;
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? ParentId { get; init; }
    public string? CurrentActivityId { get; init; }
    public string? CurrentActivityName { get; init; }
    public string? Name { get; init; }
    public bool IsActive { get; init; }
    public bool IsEnded { get; init; }
    public string? BusinessKey { get; init; }
}
