using AsterERP.Workflow.BpmnModel;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IBpmnModelService
{
    Task<bool> CheckActivitySubprocessByActivityIdAsync(string processDefId, string activityId, CancellationToken cancellationToken = default);
    Task<bool> CheckMultiInstanceAsync(dynamic task, CancellationToken cancellationToken = default);
    Task<List<string>> GetStrUserTaskListenersAsync(string activityId, string processDefinitionId, CancellationToken cancellationToken = default);
    Task<List<WorkflowExtensionListener>> GetUserTaskListenersAsync(string activityId, string processDefinitionId, CancellationToken cancellationToken = default);
    FlowElement? GetFlowElementByActivityIdAndProcessDefinitionId(string activityId, BpmnModelType bpmnModel);
    Task<FlowElement?> GetFlowElementByActivityIdAndProcessDefinitionIdAsync(string activityId, string processDefinitionId, CancellationToken cancellationToken = default);
    Task<string?> GetSingleCustomPropertyAsync(string activityId, string processDefinitionId, string customPropertyName, CancellationToken cancellationToken = default);
    string? GetSingleCustomProperty(string activityId, BpmnModelType bpmnModel, string customPropertyName);
    Task<List<ExtensionElement>> GetCustomPropertyAsync(string activityId, string processDefinitionId, string customPropertyName, CancellationToken cancellationToken = default);
    List<ExtensionElement> GetCustomProperty(string activityId, BpmnModelType bpmnModel, string customPropertyName);
    List<ServiceTask> FindServiceTasksByBpmnModel(BpmnModelType bpmnModel);
    List<UserTask> FindUserTasksByBpmnModel(BpmnModelType bpmnModel);
    Task<List<UserTask>> FindUserTasksByProcessDefIdAsync(string processDefId, CancellationToken cancellationToken = default);
    Task<BpmnModelType?> GetBpmnModelByProcessDefIdAsync(string processDefId, CancellationToken cancellationToken = default);
    StartEvent? FindStartFlowElement(Process process);
    Task<List<EndEvent>> FindEndFlowElementAsync(string processDefId, CancellationToken cancellationToken = default);
    Task<Activity?> FindActivityByNameAsync(string processDefId, string name, CancellationToken cancellationToken = default);
    Task<Activity?> FindActivityByIdAsync(string processDefId, string activityId, CancellationToken cancellationToken = default);
    Activity? FindActivityByBpmnModelAndId(BpmnModelType bpmnModel, string activityId);
    List<FlowElement> FindFlowElementByIds(BpmnModelType bpmnModel, List<string> activityIds);
    Task<List<FlowNode>> FindFlowNodesAsync(string processDefId, CancellationToken cancellationToken = default);
    GraphicInfo? GetGraphicInfo(BpmnModelType bpmnModel, string activityId);
}
