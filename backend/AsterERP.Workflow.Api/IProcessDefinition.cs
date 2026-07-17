namespace AsterERP.Workflow.Api;

public interface IProcessDefinition
{
    string Id { get; }
    string? Name { get; }
    string? Key { get; }
    string? Description { get; }
    int Version { get; }
    string? ResourceName { get; }
    string? DeploymentId { get; }
    string? DiagramResourceName { get; }
    bool HasStartFormKey { get; }
    string? StartFormKey { get; }
    string? TenantId { get; }
}
