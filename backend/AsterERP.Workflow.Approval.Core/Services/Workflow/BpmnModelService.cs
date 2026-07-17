using System.Linq;
using AsterERP.Workflow.BpmnModel;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;
using AsterERP.Workflow.Core.Deploy;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Approval.Core.Caching;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class BpmnModelService : IBpmnModelService
{
    private readonly CustomDeploymentCache<ProcessDefinitionCacheEntry> _customDeploymentCache;
    private readonly IRepositoryService _repositoryService;

    public BpmnModelService(
        CustomDeploymentCache<ProcessDefinitionCacheEntry> customDeploymentCache,
        IRepositoryService repositoryService)
    {
        _customDeploymentCache = customDeploymentCache;
        _repositoryService = repositoryService;
    }
    public async Task<bool> CheckActivitySubprocessByActivityIdAsync(
        string processDefId,
        string activityId,
        CancellationToken cancellationToken = default)
    {
        var activities = await FindFlowNodesByActivityIdAsync(processDefId, activityId, cancellationToken);
        return activities == null || activities.Count == 0;
    }

    private async Task<List<FlowNode>> FindFlowNodesByActivityIdAsync(
        string processDefId,
        string activityId,
        CancellationToken cancellationToken)
    {
        var bpmnModel = await GetBpmnModelByProcessDefIdAsync(processDefId, cancellationToken);
        if (bpmnModel == null) return new List<FlowNode>();
        var activities = new List<FlowNode>();
        foreach (var process in bpmnModel.Processes)
        {
            var flowElement = process.FlowElements.FirstOrDefault(f => f.Id == activityId);
            if (flowElement is FlowNode flowNode)
            {
                activities.Add(flowNode);
            }
        }
        return activities;
    }

    public async Task<bool> CheckMultiInstanceAsync(dynamic task, CancellationToken cancellationToken = default)
    {
        string processDefId = task.ProcessDefinitionId;
        string taskDefinitionKey = task.TaskDefinitionKey;
        var bpmnModel = await GetBpmnModelByProcessDefIdAsync(processDefId, cancellationToken);
        var userTasks = FindUserTasksByBpmnModel(bpmnModel);
        if (userTasks != null && userTasks.Count > 0)
        {
            foreach (var userTask in userTasks)
            {
                if (userTask.Id == taskDefinitionKey && userTask.LoopCharacteristics != null)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public async Task<List<string>> GetStrUserTaskListenersAsync(
        string activityId,
        string processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var listeners = await GetUserTaskListenersAsync(activityId, processDefinitionId, cancellationToken);
        return listeners?.Select(l => l.Implementation).ToList() ?? new List<string>();
    }

    public async Task<List<WorkflowExtensionListener>> GetUserTaskListenersAsync(
        string activityId,
        string processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var flowElement = await GetFlowElementByActivityIdAndProcessDefinitionIdAsync(
            activityId,
            processDefinitionId,
            cancellationToken);
        if (flowElement is UserTask userTask)
        {
            return userTask.TaskListeners;
        }
        return [];
    }

    public FlowElement? GetFlowElementByActivityIdAndProcessDefinitionId(string activityId, BpmnModelType bpmnModel)
    {
        if (bpmnModel == null) return null;
        foreach (var process in bpmnModel.Processes)
        {
            var flowElement = process.FlowElements.FirstOrDefault(f => f.Id == activityId);
            if (flowElement != null) return flowElement;
        }
        return null;
    }

    public async Task<FlowElement?> GetFlowElementByActivityIdAndProcessDefinitionIdAsync(
        string activityId,
        string processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        var bpmnModel = await GetBpmnModelByProcessDefIdAsync(processDefinitionId, cancellationToken);
        return GetFlowElementByActivityIdAndProcessDefinitionId(activityId, bpmnModel);
    }

    public async Task<string?> GetSingleCustomPropertyAsync(
        string activityId,
        string processDefinitionId,
        string customPropertyName,
        CancellationToken cancellationToken = default)
    {
        var customProperty = await GetCustomPropertyAsync(
            activityId,
            processDefinitionId,
            customPropertyName,
            cancellationToken);
        return GetSingleCustomPropertyValue(customProperty);
    }

    public string? GetSingleCustomProperty(string activityId, BpmnModelType bpmnModel, string customPropertyName)
    {
        var customProperty = GetCustomProperty(activityId, bpmnModel, customPropertyName);
        return GetSingleCustomPropertyValue(customProperty);
    }

    private string? GetSingleCustomPropertyValue(List<ExtensionElement>? customProperty)
    {
        if (customProperty != null && customProperty.Count > 0)
        {
            return customProperty[0].ElementText;
        }
        return null;
    }

    public async Task<List<ExtensionElement>> GetCustomPropertyAsync(
        string activityId,
        string processDefinitionId,
        string customPropertyName,
        CancellationToken cancellationToken = default)
    {
        var flowElement = await GetFlowElementByActivityIdAndProcessDefinitionIdAsync(
            activityId,
            processDefinitionId,
            cancellationToken);
        return GetExtensionElements(flowElement, customPropertyName);
    }

    public List<ExtensionElement> GetCustomProperty(string activityId, BpmnModelType bpmnModel, string customPropertyName)
    {
        var flowElement = GetFlowElementByActivityIdAndProcessDefinitionId(activityId, bpmnModel);
        return GetExtensionElements(flowElement, customPropertyName);
    }

    private List<ExtensionElement> GetExtensionElements(FlowElement? flowElement, string customPropertyName)
    {
        if (flowElement is UserTask userTask)
        {
            if (userTask.ExtensionElements.TryGetValue(customPropertyName, out var values))
            {
                return values;
            }
        }
        return [];
    }

    public List<ServiceTask> FindServiceTasksByBpmnModel(BpmnModelType bpmnModel)
    {
        var datas = new List<ServiceTask>();
        foreach (var process in bpmnModel.Processes)
        {
            datas.AddRange(process.FlowElements.OfType<ServiceTask>());
        }
        return datas;
    }

    public List<UserTask> FindUserTasksByBpmnModel(BpmnModelType bpmnModel)
    {
        var datas = new List<UserTask>();
        foreach (var process in bpmnModel.Processes)
        {
            datas.AddRange(process.FlowElements.OfType<UserTask>());
        }
        return datas;
    }

    public async Task<List<UserTask>> FindUserTasksByProcessDefIdAsync(
        string processDefId,
        CancellationToken cancellationToken = default)
    {
        var bpmnModel = await GetBpmnModelByProcessDefIdAsync(processDefId, cancellationToken);
        return FindUserTasksByBpmnModel(bpmnModel);
    }

    public async Task<BpmnModelType?> GetBpmnModelByProcessDefIdAsync(
        string processDefId,
        CancellationToken cancellationToken = default)
    {
        BpmnModelType? bpmnModel = null;
        if (_customDeploymentCache.Contains(processDefId))
        {
            var processDefinitionCacheEntry = _customDeploymentCache.Get(processDefId);
            if (processDefinitionCacheEntry != null)
            {
                bpmnModel = processDefinitionCacheEntry.BpmnModel;
            }
        }
        else
        {
            bpmnModel = await _repositoryService.GetBpmnModelAsync(processDefId, cancellationToken);
        }
        return bpmnModel;
    }

    public StartEvent? FindStartFlowElement(Process process)
    {
        var startEvents = process.FlowElements.OfType<StartEvent>().ToList();
        return startEvents.Count > 0 ? startEvents[0] : null;
    }

    public async Task<List<EndEvent>> FindEndFlowElementAsync(
        string processDefId,
        CancellationToken cancellationToken = default)
    {
        var bpmnModel = await GetBpmnModelByProcessDefIdAsync(processDefId, cancellationToken);
        if (bpmnModel != null)
        {
            var mainProcess = bpmnModel.Processes.FirstOrDefault();
            if (mainProcess != null)
                return mainProcess.FlowElements.OfType<EndEvent>().ToList();
        }
        return [];
    }

    public async Task<Activity?> FindActivityByNameAsync(
        string processDefId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var bpmnModel = await GetBpmnModelByProcessDefIdAsync(processDefId, cancellationToken);
        if (bpmnModel == null) return null;
        var mainProcess = bpmnModel.Processes.FirstOrDefault();
        if (mainProcess == null) return null;
        foreach (var f in mainProcess.FlowElements)
        {
            if (!string.IsNullOrWhiteSpace(name) && name == f.Name)
            {
                return f as Activity;
            }
        }
        return null;
    }

    public async Task<Activity?> FindActivityByIdAsync(
        string processDefId,
        string activityId,
        CancellationToken cancellationToken = default)
    {
        var bpmnModel = await GetBpmnModelByProcessDefIdAsync(processDefId, cancellationToken);
        if (bpmnModel == null) return null;
        var mainProcess = bpmnModel.Processes.FirstOrDefault();
        if (mainProcess == null) return null;
        foreach (var f in mainProcess.FlowElements)
        {
            if (!string.IsNullOrWhiteSpace(activityId) && f.Id == activityId)
            {
                return f as Activity;
            }
        }
        return null;
    }

    public Activity? FindActivityByBpmnModelAndId(BpmnModelType bpmnModel, string activityId)
    {
        if (bpmnModel == null) return null;
        var mainProcess = bpmnModel.Processes.FirstOrDefault();
        if (mainProcess == null) return null;
        foreach (var f in mainProcess.FlowElements)
        {
            if (!string.IsNullOrWhiteSpace(activityId) && f.Id == activityId)
            {
                return f as Activity;
            }
        }
        return null;
    }

    public List<FlowElement> FindFlowElementByIds(BpmnModelType bpmnModel, List<string> activityIds)
    {
        var flowElements = new List<FlowElement>();
        if (bpmnModel == null || activityIds == null || activityIds.Count == 0) return flowElements;
        var mainProcess = bpmnModel.Processes.FirstOrDefault();
        if (mainProcess == null) return flowElements;
        foreach (var f in mainProcess.FlowElements)
        {
            if (activityIds.Contains(f.Id))
            {
                flowElements.Add(f);
            }
        }
        return flowElements;
    }

    public async Task<List<FlowNode>> FindFlowNodesAsync(
        string processDefId,
        CancellationToken cancellationToken = default)
    {
        var flowNodes = new List<FlowNode>();
        var bpmnModel = await GetBpmnModelByProcessDefIdAsync(processDefId, cancellationToken);
        if (bpmnModel == null) return flowNodes;
        var mainProcess = bpmnModel.Processes.FirstOrDefault();
        if (mainProcess == null) return flowNodes;
        foreach (var flowElement in mainProcess.FlowElements)
        {
            if (flowElement is FlowNode flowNode)
            {
                flowNodes.Add(flowNode);
            }
        }
        return flowNodes;
    }

    public GraphicInfo? GetGraphicInfo(BpmnModelType bpmnModel, string activityId)
    {
        if (bpmnModel?.LocationMap != null && bpmnModel.LocationMap.TryGetValue(activityId, out var info))
        {
            return info;
        }
        return null;
    }
}
