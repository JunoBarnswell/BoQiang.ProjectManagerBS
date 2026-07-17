namespace AsterERP.Workflow.Core.Variable;

public class BooleanType : IVariableType
{
    public string TypeName => "boolean";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is bool;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.LongValue = value is bool b && b ? 1L : 0L;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.LongValue.HasValue)
            return variableInstance.LongValue.Value == 1L;
        return null;
    }

    public string? GetTypeForValue(object? value) => TypeName;
}
