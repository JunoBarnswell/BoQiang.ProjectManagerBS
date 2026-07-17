using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.DynamicBpmn;

public interface IDynamicBpmnService
{
    BpmnModelNs.BpmnModel GetBpmnModel(string processDefinitionId);
    void ChangeUserTaskAssignee(string processDefinitionId, string taskId, string? assignee);
    void ChangeUserTaskCandidateUsers(string processDefinitionId, string taskId, List<string> candidateUsers);
    void ChangeUserTaskCandidateGroups(string processDefinitionId, string taskId, List<string> candidateGroups);
    void ChangeUserTaskName(string processDefinitionId, string taskId, string? name);
    void ChangeUserTaskDescription(string processDefinitionId, string taskId, string? description);
    void ChangeUserTaskDueDate(string processDefinitionId, string taskId, string? dueDate);
    void ChangeUserTaskPriority(string processDefinitionId, string taskId, int? priority);
    void ChangeUserTaskFormKey(string processDefinitionId, string taskId, string? formKey);
    void ChangeServiceTaskImplementation(string processDefinitionId, string taskId, string? implementation);
    void ChangeSequenceFlowCondition(string processDefinitionId, string sequenceFlowId, string? condition);
    void AddFlowElement(string processDefinitionId, BpmnModelNs.FlowElement flowElement);
    void RemoveFlowElement(string processDefinitionId, string flowElementId);
    void AddSequenceFlow(string processDefinitionId, string sourceRef, string targetRef, string? condition = null);
    void RemoveSequenceFlow(string processDefinitionId, string sequenceFlowId);
    string GetProcessDefinitionDiagram(string processDefinitionId);
    BpmnModelNs.BpmnModel GetProcessDefinitionModel(string processDefinitionId);
    string? GetScriptTaskScript(string activityId, Dictionary<string, object> variables);
    void ChangeScriptTaskScript(string processDefinitionId, string activityId, string? script);
}
