namespace AsterERP.Workflow.Core.Variable;

public interface IVariableType
{
    string TypeName { get; }
    bool IsCachable { get; }
    bool IsAbleToStore(object? value);
    void SetValue(object? value, VariableInstanceEntity variableInstance);
    object? GetValue(VariableInstanceEntity variableInstance);
    string? GetTypeForValue(object? value);
}
