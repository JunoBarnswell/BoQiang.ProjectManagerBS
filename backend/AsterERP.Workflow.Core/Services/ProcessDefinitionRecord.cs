namespace AsterERP.Workflow.Core.Services;

public record ProcessDefinitionRecord
{
    public string Id { get; init; } = null!;
    public string? Key { get; init; }
    public string? Name { get; init; }
    public string? DeploymentId { get; init; }
    public int Version { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }
    public bool IsSuspended { get; set; }
    public string? TenantId { get; init; }
    public string? BpmnModelId { get; init; }
}
