namespace AsterERP.Workflow.Core.Behavior;

public interface IProcessDefinitionManager
{
    Task<ProcessDefinition?> FindDeployedLatestProcessDefinitionByKeyAsync(string processDefinitionKey, string? tenantId, CancellationToken cancellationToken = default);

    Task<ProcessDefinition?> FindDeployedProcessDefinitionByKeyAndVersionAsync(string processDefinitionKey, int version, string? tenantId, CancellationToken cancellationToken = default);

    Task<ProcessDefinition?> FindDeployedProcessDefinitionByDeploymentIdAndKeyAsync(string deploymentId, string processDefinitionKey, string? tenantId, CancellationToken cancellationToken = default);
}
