using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Job;

namespace AsterERP.Workflow.Core.Services;

public interface IRepositoryService
{
    Task<string> CreateDeploymentAsync(CancellationToken cancellationToken = default);
    Task<string> DeployAsync(string? name, string? category, string? tenantId, Dictionary<string, byte[]> resources, bool enableDuplicateFiltering = false, CancellationToken cancellationToken = default);
    Task DeleteDeploymentAsync(string deploymentId, bool cascade, CancellationToken cancellationToken = default);
    Task SetDeploymentCategoryAsync(string deploymentId, string? category, CancellationToken cancellationToken = default);
    Task SetDeploymentKeyAsync(string deploymentId, string? key, CancellationToken cancellationToken = default);
    Task ChangeDeploymentTenantIdAsync(string deploymentId, string newTenantId, CancellationToken cancellationToken = default);
    Task<ProcessDefinitionRecord?> GetProcessDefinitionByIdAsync(string processDefinitionId, CancellationToken cancellationToken = default);
    Task<List<ProcessDefinitionRecord>> GetProcessDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsProcessDefinitionSuspendedAsync(string processDefinitionId, CancellationToken cancellationToken = default);
    Task SuspendProcessDefinitionByIdAsync(string processDefinitionId, CancellationToken cancellationToken = default);
    Task SuspendProcessDefinitionByIdAsync(string processDefinitionId, bool suspendProcessInstances, DateTime? suspensionDate, CancellationToken cancellationToken = default);
    Task SuspendProcessDefinitionByKeyAsync(string processDefinitionKey, CancellationToken cancellationToken = default);
    Task SuspendProcessDefinitionByKeyAsync(string processDefinitionKey, bool suspendProcessInstances, DateTime? suspensionDate, CancellationToken cancellationToken = default);
    Task ActivateProcessDefinitionByIdAsync(string processDefinitionId, CancellationToken cancellationToken = default);
    Task ActivateProcessDefinitionByIdAsync(string processDefinitionId, bool activateProcessInstances, DateTime? activationDate, CancellationToken cancellationToken = default);
    Task ActivateProcessDefinitionByKeyAsync(string processDefinitionKey, CancellationToken cancellationToken = default);
    Task ActivateProcessDefinitionByKeyAsync(string processDefinitionKey, bool activateProcessInstances, DateTime? activationDate, CancellationToken cancellationToken = default);
    Task SetProcessDefinitionCategoryAsync(string processDefinitionId, string? category, CancellationToken cancellationToken = default);
    Task<byte[]?> GetResourceAsync(string deploymentId, string resourceName, CancellationToken cancellationToken = default);
    Task<List<string>> GetDeploymentResourceNamesAsync(string deploymentId, CancellationToken cancellationToken = default);
    Task<byte[]?> GetProcessModelAsync(string processDefinitionId, CancellationToken cancellationToken = default);
    Task<BpmnModel.BpmnModel> GetBpmnModelAsync(string processDefinitionId, CancellationToken cancellationToken = default);
    Task<ModelEntity> NewModelAsync(CancellationToken cancellationToken = default);
    Task SaveModelAsync(ModelEntity model, CancellationToken cancellationToken = default);
    Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default);
    Task<ModelEntity?> GetModelAsync(string modelId, CancellationToken cancellationToken = default);
    Task<byte[]?> GetModelEditorSourceAsync(string modelId, CancellationToken cancellationToken = default);
    Task<byte[]?> GetModelEditorSourceExtraAsync(string modelId, CancellationToken cancellationToken = default);
    Task AddModelEditorSourceAsync(string modelId, byte[] editorSource, CancellationToken cancellationToken = default);
    Task AddModelEditorSourceExtraAsync(string modelId, byte[] editorSourceExtra, CancellationToken cancellationToken = default);
    Task AddCandidateStarterUserAsync(string processDefinitionId, string userId, CancellationToken cancellationToken = default);
    Task AddCandidateStarterGroupAsync(string processDefinitionId, string groupId, CancellationToken cancellationToken = default);
    Task DeleteCandidateStarterUserAsync(string processDefinitionId, string userId, CancellationToken cancellationToken = default);
    Task DeleteCandidateStarterGroupAsync(string processDefinitionId, string groupId, CancellationToken cancellationToken = default);
    Task<List<IdentityLinkEntity>> GetIdentityLinksForProcessDefinitionAsync(string processDefinitionId, CancellationToken cancellationToken = default);
    Task<List<ValidationError>> ValidateProcessAsync(byte[]? bpmnXml, CancellationToken cancellationToken = default);
}
