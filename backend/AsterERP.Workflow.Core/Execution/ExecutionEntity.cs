using System;
using System.Collections.Generic;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Services;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Execution;

public interface IExecution
{
    string Id { get; }
    string? ProcessInstanceId { get; }
    string? ProcessDefinitionId { get; }
    string? ParentId { get; }
    string? CurrentActivityId { get; }
    string? CurrentActivityName { get; }
    string? ActivityId { get; }
    bool IsActive { get; }
    bool IsEnded { get; }
    bool IsProcessInstanceType { get; }
    bool IsScope { get; }
    string? TenantId { get; }
    string? EventName { get; }
    string? BusinessKey { get; }
}

public interface IExecutionManager
{
    global::System.Threading.Tasks.Task<ExecutionEntity> CreateExecutionAsync(string processDefinitionId, string? processInstanceId = null, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<ExecutionEntity?> FindByIdAsync(string executionId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<List<ExecutionEntity>> FindByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task SaveAsync(ExecutionEntity execution, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task DeleteAsync(string executionId, CancellationToken cancellationToken = default);
}

public class ExecutionEntity : IExecution, IDelegateExecution
{
    public string Id { get; set; } = null!;
    public int Revision { get; set; }
    public string? ProcessInstanceId { get; set; }
    public string? ProcessDefinitionId { get; set; }
    public string? ParentId { get; set; }
    public string? SuperExecutionId { get; set; }
    public string? CurrentActivityId { get; set; }
    public string? CurrentActivityName { get; set; }
    public string? ActivityId { get; set; }
    public string? CurrentFlowElementId { get; set; }
    public BpmnModelNs.FlowElement? CurrentFlowElement { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEnded { get; set; }
    public bool IsProcessInstanceType { get; set; }
    public bool IsScope { get; set; } = true;
    public bool IsConcurrent { get; set; }
    public string? TenantId { get; set; }
    public string? EventName { get; set; }
    public string? BusinessKey { get; set; }

    public Dictionary<string, object?> Variables { get; set; } = new();
    public List<ExecutionEntity> ChildExecutions { get; set; } = new();
    public ExecutionEntity? Parent { get; set; }
    public List<TaskImplementation> TaskEntities { get; set; } = new();
    public BpmnModelNs.Process? Process { get; set; }

    public object? GetVariable(string name)
    {
        return Variables.GetValueOrDefault(name);
    }

    public void SetVariable(string name, object? value)
    {
        Variables[name] = value;
    }

    public object? GetVariableLocal(string name)
    {
        return Variables.GetValueOrDefault(name);
    }

    public void SetVariableLocal(string name, object? value)
    {
        Variables[name] = value;
    }

    public bool HasVariable(string variableName)
    {
        return Variables.ContainsKey(variableName);
    }

    public bool HasVariableLocal(string variableName)
    {
        return Variables.ContainsKey(variableName);
    }

    public void RemoveVariable(string variableName)
    {
        Variables.Remove(variableName);
    }

    public void RemoveVariableLocal(string variableName)
    {
        Variables.Remove(variableName);
    }

    public void SetVariables(Dictionary<string, object?> variables)
    {
        foreach (var kvp in variables)
            Variables[kvp.Key] = kvp.Value;
    }

    public void SetVariablesLocal(Dictionary<string, object?> variables)
    {
        foreach (var kvp in variables)
            Variables[kvp.Key] = kvp.Value;
    }
}
