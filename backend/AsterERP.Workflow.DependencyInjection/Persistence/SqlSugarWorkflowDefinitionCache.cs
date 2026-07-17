using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.Core.Deployer;
using AsterERP.Workflow.Core.Deploy;
using AsterERP.Workflow.Core.Engine;
using PersistentProcessDefinitionEntity = AsterERP.Workflow.Persistence.Entities.ProcessDefinitionEntity;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

internal static class SqlSugarWorkflowDefinitionCache
{
    public static void RegisterProcessDefinition(
        IProcessEngineConfiguration processEngineConfiguration,
        PersistentProcessDefinitionEntity definition,
        AsterERP.Workflow.BpmnModel.BpmnModel bpmnModel,
        Process processModel)
    {
        var deploymentManager = EnsureProcessDefinitionCache(processEngineConfiguration);

        var cacheEntry = new ProcessDefinitionCacheEntry(
            new ProcessDefinitionInfo
            {
                Id = definition.Id,
                Key = definition.Key ?? processModel.Id ?? definition.Id,
                Name = definition.Name ?? processModel.Name,
                Category = definition.Category,
                Description = definition.Description,
                Version = definition.Version,
                DeploymentId = definition.DeploymentId,
                ResourceName = definition.ResourceName,
                TenantId = definition.TenantId,
                IsSuspended = definition.SuspensionState != 1,
                DiagramResourceName = definition.DiagramResourceName,
                HasStartFormKey = definition.HasStartFormKey,
                BpmnModel = bpmnModel
            },
            bpmnModel,
            processModel);

        deploymentManager.ProcessDefinitionCache.Add(definition.Id, cacheEntry);
    }

    public static DeploymentManager EnsureProcessDefinitionCache(IProcessEngineConfiguration processEngineConfiguration)
    {
        var deploymentManager = processEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
        {
            deploymentManager = new DeploymentManager();
            processEngineConfiguration.DeploymentManager = deploymentManager;
        }

        if (deploymentManager.ProcessDefinitionCache == null)
        {
            deploymentManager.ProcessDefinitionCache = new DefaultDeploymentCache<ProcessDefinitionCacheEntry>();
        }

        return deploymentManager;
    }

    public static Dictionary<string, Process> BuildProcessMap(IProcessEngineConfiguration processEngineConfiguration)
    {
        var map = new Dictionary<string, Process>(StringComparer.Ordinal);
        var deploymentManager = processEngineConfiguration.DeploymentManager;
        if (deploymentManager?.ProcessDefinitionCache == null)
        {
            return map;
        }

        foreach (var entry in deploymentManager.ProcessDefinitionCache.GetAll())
        {
            if (!string.IsNullOrWhiteSpace(entry.ProcessDefinition.Id) && entry.Process != null)
            {
                map[entry.ProcessDefinition.Id] = entry.Process;
            }
        }

        return map;
    }

    public static FlowElement? FindFlowElement(Process process, string? flowElementId)
    {
        if (string.IsNullOrWhiteSpace(flowElementId))
        {
            return null;
        }

        return FlattenFlowElements(process).FirstOrDefault(flowElement => flowElement.Id == flowElementId);
    }

    public static int ResolveDefinitionVersion(string processDefinitionId)
    {
        var segments = processDefinitionId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 1 && int.TryParse(segments[^1], out var version)
            ? version
            : 1;
    }

    private static IEnumerable<FlowElement> FlattenFlowElements(IFlowElementsContainer container)
    {
        foreach (var flowElement in container.FlowElements)
        {
            yield return flowElement;
            if (flowElement is SubProcess subProcess)
            {
                foreach (var nested in FlattenFlowElements(subProcess))
                {
                    yield return nested;
                }
            }
        }
    }
}
