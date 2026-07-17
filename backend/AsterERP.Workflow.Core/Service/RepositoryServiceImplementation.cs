using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Deploy;
using AsterERP.Workflow.Core.Deployer;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Service;

public class RepositoryServiceImplementation : ServiceImpl, IRepositoryService
{
    public RepositoryServiceImplementation() : base(AsterERP.Workflow.Core.Engine.ProcessEngineConfiguration.CreateDefault()) { }

    public RepositoryServiceImplementation(IProcessEngineConfiguration processEngineConfiguration)
        : base(processEngineConfiguration) { }

    public RepositoryServiceImplementation(ICommandExecutor commandExecutor)
        : base(commandExecutor) { }

    public async Task<string> CreateDeploymentAsync(CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new DeployCmd(null, null, null, new Dictionary<string, byte[]>()), cancellationToken);
        return result.Id;
    }

    public async Task<string> DeployAsync(string? name, string? category, string? tenantId, Dictionary<string, byte[]> resources, bool enableDuplicateFiltering = false, CancellationToken cancellationToken = default)
    {
        foreach (var resource in resources)
        {
            if (IsBpmnResource(resource.Key))
            {
                BpmnXmlSecurity.Validate(resource.Value);
            }
        }

        var result = await CommandExecutor.ExecuteAsync(
            new DeployCmd(name, category, tenantId, resources, enableDuplicateFiltering), cancellationToken);
        return result.Id;
    }

    public async Task DeleteDeploymentAsync(string deploymentId, bool cascade, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteDeploymentCmd(deploymentId, cascade), cancellationToken);
    }

    public async Task SetDeploymentCategoryAsync(string deploymentId, string? category, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetDeploymentCategoryCmd(deploymentId, category), cancellationToken);
    }

    public async Task SetDeploymentKeyAsync(string deploymentId, string? key, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetDeploymentKeyCmd(deploymentId, key), cancellationToken);
    }

    public async Task ChangeDeploymentTenantIdAsync(string deploymentId, string newTenantId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new ChangeDeploymentTenantIdCmd(deploymentId, newTenantId), cancellationToken);
    }

    public async Task<ProcessDefinitionRecord?> GetProcessDefinitionByIdAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetDeploymentProcessDefinitionCmd(processDefinitionId), cancellationToken);
    }

    public async Task<List<ProcessDefinitionRecord>> GetProcessDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetProcessDefinitionsCmd(), cancellationToken);
    }

    public async Task<bool> IsProcessDefinitionSuspendedAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new IsProcessDefinitionSuspendedCmd(processDefinitionId), cancellationToken);
    }

    public async Task SuspendProcessDefinitionByIdAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new SuspendProcessDefinitionCmd(processDefinitionId, null, false, null, null), cancellationToken);
    }

    public async Task SuspendProcessDefinitionByIdAsync(string processDefinitionId, bool suspendProcessInstances, DateTime? suspensionDate, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new SuspendProcessDefinitionCmd(processDefinitionId, null, suspendProcessInstances, suspensionDate, null), cancellationToken);
    }

    public async Task SuspendProcessDefinitionByKeyAsync(string processDefinitionKey, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new SuspendProcessDefinitionCmd(null, processDefinitionKey, false, null, null), cancellationToken);
    }

    public async Task SuspendProcessDefinitionByKeyAsync(string processDefinitionKey, bool suspendProcessInstances, DateTime? suspensionDate, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new SuspendProcessDefinitionCmd(null, processDefinitionKey, suspendProcessInstances, suspensionDate, null), cancellationToken);
    }

    public async Task ActivateProcessDefinitionByIdAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new ActivateProcessDefinitionCmd(processDefinitionId, null, false, null, null), cancellationToken);
    }

    public async Task ActivateProcessDefinitionByIdAsync(string processDefinitionId, bool activateProcessInstances, DateTime? activationDate, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new ActivateProcessDefinitionCmd(processDefinitionId, null, activateProcessInstances, activationDate, null), cancellationToken);
    }

    public async Task ActivateProcessDefinitionByKeyAsync(string processDefinitionKey, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new ActivateProcessDefinitionCmd(null, processDefinitionKey, false, null, null), cancellationToken);
    }

    public async Task ActivateProcessDefinitionByKeyAsync(string processDefinitionKey, bool activateProcessInstances, DateTime? activationDate, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new ActivateProcessDefinitionCmd(null, processDefinitionKey, activateProcessInstances, activationDate, null), cancellationToken);
    }

    public async Task SetProcessDefinitionCategoryAsync(string processDefinitionId, string? category, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetProcessDefinitionCategoryCmd(processDefinitionId, category), cancellationToken);
    }

    public async Task<byte[]?> GetResourceAsync(string deploymentId, string resourceName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetDeploymentResourceCmd(deploymentId, resourceName), cancellationToken);
    }

    public async Task<List<string>> GetDeploymentResourceNamesAsync(string deploymentId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetDeploymentResourceNamesCmd(deploymentId), cancellationToken);
    }

    public async Task<byte[]?> GetProcessModelAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetDeploymentProcessModelCmd(processDefinitionId), cancellationToken);
    }

    public async Task<BpmnModel.BpmnModel> GetBpmnModelAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetBpmnModelCmd(processDefinitionId), cancellationToken);
    }

    public async Task<ModelEntity> NewModelAsync(CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new CreateModelCmd(), cancellationToken);
    }

    public async Task SaveModelAsync(ModelEntity model, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SaveModelCmd(model), cancellationToken);
    }

    public async Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteModelCmd(modelId), cancellationToken);
    }

    public async Task<ModelEntity?> GetModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetModelCmd(modelId), cancellationToken);
    }

    public async Task<byte[]?> GetModelEditorSourceAsync(string modelId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetModelEditorSourceCmd(modelId), cancellationToken);
    }

    public async Task<byte[]?> GetModelEditorSourceExtraAsync(string modelId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetModelEditorSourceExtraCmd(modelId), cancellationToken);
    }

    public async Task AddModelEditorSourceAsync(string modelId, byte[] editorSource, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new AddEditorSourceForModelCmd(modelId, editorSource), cancellationToken);
    }

    public async Task AddModelEditorSourceExtraAsync(string modelId, byte[] editorSourceExtra, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new AddEditorSourceExtraForModelCmd(modelId, editorSourceExtra), cancellationToken);
    }

    public async Task AddCandidateStarterUserAsync(string processDefinitionId, string userId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkForProcessDefinitionCmd(processDefinitionId, userId, null, IdentityLinkType.CANDIDATE), cancellationToken);
    }

    public async Task AddCandidateStarterGroupAsync(string processDefinitionId, string groupId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkForProcessDefinitionCmd(processDefinitionId, null, groupId, IdentityLinkType.CANDIDATE), cancellationToken);
    }

    public async Task DeleteCandidateStarterUserAsync(string processDefinitionId, string userId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new DeleteIdentityLinkForProcessDefinitionCmd(processDefinitionId, userId, null, IdentityLinkType.CANDIDATE), cancellationToken);
    }

    public async Task DeleteCandidateStarterGroupAsync(string processDefinitionId, string groupId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new DeleteIdentityLinkForProcessDefinitionCmd(processDefinitionId, null, groupId, IdentityLinkType.CANDIDATE), cancellationToken);
    }

    public async Task<List<IdentityLinkEntity>> GetIdentityLinksForProcessDefinitionAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processDefinitionId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("processDefinitionId is null");
        return await CommandExecutor.ExecuteAsync(
            new GetIdentityLinksForProcessDefinitionCmd(processDefinitionId),
            cancellationToken);
    }

    public async Task<List<ValidationError>> ValidateProcessAsync(byte[]? bpmnXml, CancellationToken cancellationToken = default)
    {
        try
        {
            BpmnXmlSecurity.Validate(bpmnXml);
        }
        catch (XmlException exception)
        {
            return
            [
                new ValidationError
                {
                    Message = $"Invalid BPMN XML: {exception.Message}",
                    Type = "BPMN_SECURITY_VALIDATION",
                    IsWarning = false
                }
            ];
        }

        return await CommandExecutor.ExecuteAsync(new ValidateBpmnModelCmd(bpmnXml), cancellationToken);
    }

    public void RegisterProcessDefinition(string processDefinitionId, string deploymentId, BpmnModel.BpmnModel bpmnModel, BpmnModel.Process process)
    {
        var deploymentManager = ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
        {
            deploymentManager = new DeploymentManager();
            ProcessEngineConfiguration.DeploymentManager = deploymentManager;
        }

        if (deploymentManager.ProcessDefinitionCache == null)
        {
            deploymentManager.ProcessDefinitionCache = new DefaultDeploymentCache<ProcessDefinitionCacheEntry>();
        }

        var key = process?.Id ?? processDefinitionId.Split(':').FirstOrDefault() ?? processDefinitionId;
        var cacheEntry = new ProcessDefinitionCacheEntry(
            new Deployer.ProcessDefinitionInfo
            {
                Id = processDefinitionId,
                Key = key,
                Name = process?.Name,
                DeploymentId = deploymentId,
                Version = 1,
                IsSuspended = false,
                TenantId = null,
                BpmnModel = bpmnModel
            },
            bpmnModel,
            process);

        deploymentManager.ProcessDefinitionCache.Add(processDefinitionId, cacheEntry);
    }

    public Deployer.ProcessDefinitionInfo? GetProcessDefinitionByKey(string key)
    {
        var deploymentManager = ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager?.ProcessDefinitionCache == null)
            return null;

        foreach (var entry in deploymentManager.ProcessDefinitionCache.GetAll())
        {
            if (entry.ProcessDefinition.Key == key)
                return entry.ProcessDefinition;
        }

        return null;
    }

    private static bool IsBpmnResource(string resourceName) =>
        resourceName.EndsWith(".bpmn", StringComparison.OrdinalIgnoreCase) ||
        resourceName.EndsWith(".bpmn20.xml", StringComparison.OrdinalIgnoreCase);
}
