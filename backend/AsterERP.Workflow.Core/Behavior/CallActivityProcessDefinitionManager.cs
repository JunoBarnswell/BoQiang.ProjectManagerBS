using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Service;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Behavior;

public sealed class CallActivityProcessDefinitionManager : IProcessDefinitionManager
{
    private readonly RepositoryServiceImplementation _repositoryService;

    public CallActivityProcessDefinitionManager(IRepositoryService repositoryService)
    {
        _repositoryService = (RepositoryServiceImplementation)repositoryService;
    }

    public async Task<ProcessDefinition?> FindDeployedLatestProcessDefinitionByKeyAsync(
        string processDefinitionKey,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var definitions = await _repositoryService.GetProcessDefinitionsAsync(cancellationToken);
        var latest = definitions
            .Where(definition => definition.Key == processDefinitionKey && (tenantId == null || definition.TenantId == tenantId))
            .OrderByDescending(definition => definition.Version)
            .ThenByDescending(definition => definition.Id, System.StringComparer.Ordinal)
            .FirstOrDefault();

        return latest != null ? Map(latest) : null;
    }

    public async Task<ProcessDefinition?> FindDeployedProcessDefinitionByKeyAndVersionAsync(
        string processDefinitionKey,
        int version,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var definitions = await _repositoryService.GetProcessDefinitionsAsync(cancellationToken);
        var match = definitions.FirstOrDefault(definition =>
            definition.Key == processDefinitionKey &&
            definition.Version == version &&
            (tenantId == null || definition.TenantId == tenantId));

        return match != null ? Map(match) : null;
    }

    public async Task<ProcessDefinition?> FindDeployedProcessDefinitionByDeploymentIdAndKeyAsync(
        string deploymentId,
        string processDefinitionKey,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var definitions = await _repositoryService.GetProcessDefinitionsAsync(cancellationToken);
        var match = definitions.FirstOrDefault(definition =>
            definition.DeploymentId == deploymentId &&
            definition.Key == processDefinitionKey &&
            (tenantId == null || definition.TenantId == tenantId));

        return match != null ? Map(match) : null;
    }

    private static ProcessDefinition Map(ProcessDefinitionRecord definition)
    {
        return new ProcessDefinition
        {
            Id = definition.Id,
            Key = definition.Key,
            Name = definition.Name,
            Version = definition.Version,
            DeploymentId = definition.DeploymentId,
            TenantId = definition.TenantId,
            IsSuspended = definition.IsSuspended,
            Description = definition.Description
        };
    }
}
