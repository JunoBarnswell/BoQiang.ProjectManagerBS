using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.DynamicBpmn;

public class DynamicBpmnServiceImplementation : IDynamicBpmnService
{
    private readonly Dictionary<string, BpmnModelNs.BpmnModel> _models = new();
    private readonly ProcessDefinitionChangeAudit _audit = new();

    public ProcessDefinitionChangeAudit Audit => _audit;

    public void RegisterModel(string processDefinitionId, BpmnModelNs.BpmnModel model)
    {
        _models[processDefinitionId] = model;
    }

    public void UnregisterModel(string processDefinitionId)
    {
        _models.Remove(processDefinitionId);
    }

    public BpmnModelNs.BpmnModel GetBpmnModel(string processDefinitionId)
    {
        if (!_models.TryGetValue(processDefinitionId, out var model))
            throw new InvalidOperationException($"No BpmnModel registered for process definition id: {processDefinitionId}");
        return model;
    }

    public void ChangeUserTaskAssignee(string processDefinitionId, string taskId, string? assignee)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.UserTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"UserTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = task.Assignee;
        task.Assignee = assignee;
        _audit.RecordChange(processDefinitionId, "ChangeUserTaskAssignee", taskId, oldValue, assignee);
    }

    public void ChangeUserTaskCandidateUsers(string processDefinitionId, string taskId, List<string> candidateUsers)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.UserTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"UserTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = string.Join(",", task.CandidateUsers);
        task.CandidateUsers.Clear();
        task.CandidateUsers.AddRange(candidateUsers);
        var newValue = string.Join(",", candidateUsers);
        _audit.RecordChange(processDefinitionId, "ChangeUserTaskCandidateUsers", taskId, oldValue, newValue);
    }

    public void ChangeUserTaskCandidateGroups(string processDefinitionId, string taskId, List<string> candidateGroups)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.UserTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"UserTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = string.Join(",", task.CandidateGroups);
        task.CandidateGroups.Clear();
        task.CandidateGroups.AddRange(candidateGroups);
        var newValue = string.Join(",", candidateGroups);
        _audit.RecordChange(processDefinitionId, "ChangeUserTaskCandidateGroups", taskId, oldValue, newValue);
    }

    public void ChangeUserTaskName(string processDefinitionId, string taskId, string? name)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.UserTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"UserTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = task.Name;
        task.Name = name;
        _audit.RecordChange(processDefinitionId, "ChangeUserTaskName", taskId, oldValue, name);
    }

    public void ChangeUserTaskDescription(string processDefinitionId, string taskId, string? description)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.UserTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"UserTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = task.Documentation;
        task.Documentation = description;
        _audit.RecordChange(processDefinitionId, "ChangeUserTaskDescription", taskId, oldValue, description);
    }

    public void ChangeUserTaskDueDate(string processDefinitionId, string taskId, string? dueDate)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.UserTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"UserTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = task.GetAttributeValue("http://AsterERP.Workflow.org/bpmn", "dueDate");
        if (dueDate != null)
            task.AddAttribute(new BpmnModelNs.ExtensionAttribute { Namespace = "http://AsterERP.Workflow.org/bpmn", Name = "dueDate", Value = dueDate, NamespacePrefix = "activiti" });
        else
            task.Attributes.Remove("dueDate");
        _audit.RecordChange(processDefinitionId, "ChangeUserTaskDueDate", taskId, oldValue, dueDate);
    }

    public void ChangeUserTaskPriority(string processDefinitionId, string taskId, int? priority)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.UserTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"UserTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = task.Priority?.ToString();
        task.Priority = priority;
        _audit.RecordChange(processDefinitionId, "ChangeUserTaskPriority", taskId, oldValue, priority?.ToString());
    }

    public void ChangeUserTaskFormKey(string processDefinitionId, string taskId, string? formKey)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.UserTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"UserTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = task.FormKey;
        task.FormKey = formKey;
        _audit.RecordChange(processDefinitionId, "ChangeUserTaskFormKey", taskId, oldValue, formKey);
    }

    public void ChangeServiceTaskImplementation(string processDefinitionId, string taskId, string? implementation)
    {
        var model = GetBpmnModel(processDefinitionId);
        var task = FindFlowElement<BpmnModelNs.ServiceTask>(model, taskId);
        if (task == null) throw new InvalidOperationException($"ServiceTask with id '{taskId}' not found in process '{processDefinitionId}'");

        var oldValue = task.Implementation;
        task.Implementation = implementation;
        _audit.RecordChange(processDefinitionId, "ChangeServiceTaskImplementation", taskId, oldValue, implementation);
    }

    public void ChangeSequenceFlowCondition(string processDefinitionId, string sequenceFlowId, string? condition)
    {
        var model = GetBpmnModel(processDefinitionId);
        var sf = model.GetSequenceFlow(sequenceFlowId);
        if (sf == null) throw new InvalidOperationException($"SequenceFlow with id '{sequenceFlowId}' not found in process '{processDefinitionId}'");

        var oldValue = sf.ConditionExpression;
        sf.ConditionExpression = condition;
        _audit.RecordChange(processDefinitionId, "ChangeSequenceFlowCondition", sequenceFlowId, oldValue, condition);
    }

    public void AddFlowElement(string processDefinitionId, BpmnModelNs.FlowElement flowElement)
    {
        var model = GetBpmnModel(processDefinitionId);
        var process = model.GetProcessById(processDefinitionId);
        if (process == null) throw new InvalidOperationException($"Process with id '{processDefinitionId}' not found");

        process.FlowElements.Add(flowElement);
        model.FlowElementMap[$"{processDefinitionId}.{flowElement.Id}"] = flowElement;
        if (flowElement is BpmnModelNs.SequenceFlow sf && sf.Id != null)
            model.SequenceFlowMap[sf.Id] = sf;

        _audit.RecordChange(processDefinitionId, "AddFlowElement", flowElement.Id!, null, flowElement.GetType().Name);
    }

    public void RemoveFlowElement(string processDefinitionId, string flowElementId)
    {
        var model = GetBpmnModel(processDefinitionId);
        var process = model.GetProcessById(processDefinitionId);
        if (process == null) throw new InvalidOperationException($"Process with id '{processDefinitionId}' not found");

        var index = process.FlowElements.FindIndex(fe => fe.Id == flowElementId);
        if (index < 0) throw new InvalidOperationException($"FlowElement with id '{flowElementId}' not found in process '{processDefinitionId}'");

        var removed = process.FlowElements[index];
        process.FlowElements.RemoveAt(index);
        model.FlowElementMap.Remove($"{processDefinitionId}.{flowElementId}");
        if (removed is BpmnModelNs.SequenceFlow)
            model.SequenceFlowMap.Remove(flowElementId);

        _audit.RecordChange(processDefinitionId, "RemoveFlowElement", flowElementId, removed.GetType().Name, null);
    }

    public void AddSequenceFlow(string processDefinitionId, string sourceRef, string targetRef, string? condition = null)
    {
        var sfId = $"flow_{sourceRef}_{targetRef}";
        var sf = new BpmnModelNs.SequenceFlow
        {
            Id = sfId,
            SourceRef = sourceRef,
            TargetRef = targetRef,
            ConditionExpression = condition
        };
        AddFlowElement(processDefinitionId, sf);
    }

    public void RemoveSequenceFlow(string processDefinitionId, string sequenceFlowId)
    {
        RemoveFlowElement(processDefinitionId, sequenceFlowId);
    }

    public string GetProcessDefinitionDiagram(string processDefinitionId)
    {
        var model = GetBpmnModel(processDefinitionId);
        var exporter = new BpmnParser.BpmnModelExporter();
        return exporter.ExportToXml(model);
    }

    public BpmnModelNs.BpmnModel GetProcessDefinitionModel(string processDefinitionId)
    {
        return GetBpmnModel(processDefinitionId);
    }

    public string? GetScriptTaskScript(string activityId, Dictionary<string, object> variables)
    {
        foreach (var model in _models.Values)
        {
            var scriptTask = FindFlowElement<BpmnModelNs.ScriptTask>(model, activityId);
            if (scriptTask != null)
            {
                return scriptTask.Script;
            }
        }
        return null;
    }

    public void ChangeScriptTaskScript(string processDefinitionId, string activityId, string? script)
    {
        var model = GetBpmnModel(processDefinitionId);
        var scriptTask = FindFlowElement<BpmnModelNs.ScriptTask>(model, activityId);
        if (scriptTask == null) throw new InvalidOperationException($"ScriptTask with id '{activityId}' not found in process '{processDefinitionId}'");

        var oldValue = scriptTask.Script;
        scriptTask.Script = script;
        _audit.RecordChange(processDefinitionId, "ChangeScriptTaskScript", activityId, oldValue, script);
    }

    private T? FindFlowElement<T>(BpmnModelNs.BpmnModel model, string elementId) where T : BpmnModelNs.FlowElement
    {
        foreach (var process in model.Processes)
        {
            var element = FindFlowElementInContainer<T>(process.FlowElements, elementId);
            if (element != null) return element;
        }
        return null;
    }

    private T? FindFlowElementInContainer<T>(List<BpmnModelNs.FlowElement> flowElements, string elementId) where T : BpmnModelNs.FlowElement
    {
        foreach (var fe in flowElements)
        {
            if (fe is T typed && fe.Id == elementId)
                return typed;

            if (fe is BpmnModelNs.SubProcess sp)
            {
                var found = FindFlowElementInContainer<T>(sp.FlowElements, elementId);
                if (found != null) return found;
            }
        }
        return null;
    }
}
