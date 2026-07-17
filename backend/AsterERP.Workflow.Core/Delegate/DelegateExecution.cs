using System.Collections.Generic;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Delegate;

public class DelegateExecution : IDelegateExecution
{
    private readonly ExecutionEntity _execution;

    public DelegateExecution(ExecutionEntity execution)
    {
        _execution = execution;
    }

    public string Id => _execution.Id;
    public string? ProcessInstanceId => _execution.ProcessInstanceId;
    public string? ProcessDefinitionId => _execution.ProcessDefinitionId;
    public string? ParentId => _execution.ParentId;
    public string? CurrentActivityId => _execution.CurrentActivityId ?? _execution.CurrentFlowElementId ?? _execution.CurrentFlowElement?.Id ?? _execution.ActivityId;
    public string? ActivityId => _execution.ActivityId;
    public string? CurrentActivityName => _execution.CurrentActivityName ?? _execution.CurrentFlowElement?.Name;
    public bool IsActive => _execution.IsActive;
    public bool IsEnded => _execution.IsEnded;
    public bool IsProcessInstanceType => _execution.IsProcessInstanceType;
    public bool IsScope => _execution.IsScope;
    public string? TenantId => _execution.TenantId;
    public string? EventName => _execution.EventName;
    public string? BusinessKey => _execution.BusinessKey;
    public Dictionary<string, object?> Variables => _execution.Variables;

    public object? GetVariable(string name)
    {
        return _execution.GetVariable(name);
    }

    public void SetVariable(string name, object? value)
    {
        _execution.SetVariable(name, value);
    }

    public object? GetVariableLocal(string variableName)
    {
        return _execution.GetVariableLocal(variableName);
    }

    public void SetVariableLocal(string variableName, object? value)
    {
        _execution.SetVariableLocal(variableName, value);
    }

    public bool HasVariable(string variableName)
    {
        return _execution.HasVariable(variableName);
    }

    public bool HasVariableLocal(string variableName)
    {
        return _execution.HasVariableLocal(variableName);
    }

    public void RemoveVariable(string variableName)
    {
        _execution.RemoveVariable(variableName);
    }

    public void RemoveVariableLocal(string variableName)
    {
        _execution.RemoveVariableLocal(variableName);
    }

    public void SetVariables(Dictionary<string, object?> variables)
    {
        _execution.SetVariables(variables);
    }

    public void SetVariablesLocal(Dictionary<string, object?> variables)
    {
        _execution.SetVariablesLocal(variables);
    }
}
