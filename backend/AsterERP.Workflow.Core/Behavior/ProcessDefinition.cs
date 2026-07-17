namespace AsterERP.Workflow.Core.Behavior;

public class ProcessDefinition
{
    public string Id { get; set; } = null!;
    public string? Key { get; set; }
    public string? Name { get; set; }
    public int Version { get; set; }
    public string? DeploymentId { get; set; }
    public string? ResourceName { get; set; }
    public string? TenantId { get; set; }
    public bool IsSuspended { get; set; }
    public string? Description { get; set; }
}
