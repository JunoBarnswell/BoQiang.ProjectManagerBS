using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deployer;

public class ProcessDefinitionInfo
{
    public string Id { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; }
    public string? DeploymentId { get; set; }
    public string? ResourceName { get; set; }
    public string? TenantId { get; set; }
    public bool IsSuspended { get; set; }
    public string? DiagramResourceName { get; set; }
    public bool HasStartFormKey { get; set; }
    public BpmnModelType? BpmnModel { get; set; }
}
