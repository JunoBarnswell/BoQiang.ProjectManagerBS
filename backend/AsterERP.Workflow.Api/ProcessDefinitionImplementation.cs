namespace AsterERP.Workflow.Api;

public class ProcessDefinitionImplementation : IProcessDefinition
{
    public string Id { get; set; } = null!;
    public string? Name { get; set; }
    public string? Key { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; }
    public string? ResourceName { get; set; }
    public string? DeploymentId { get; set; }
    public string? DiagramResourceName { get; set; }
    public bool HasStartFormKey { get; set; }
    public string? StartFormKey { get; set; }
    public string? TenantId { get; set; }
}
