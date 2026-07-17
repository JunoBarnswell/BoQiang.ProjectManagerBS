namespace AsterERP.Workflow.Core.Variable;

public class IntegerType : IVariableType
{
    public string TypeName => "integer";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is int;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.LongValue = value is int i ? i : null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        return variableInstance.LongValue.HasValue ? (int)variableInstance.LongValue.Value : null;
    }

    public string? GetTypeForValue(object? value) => TypeName;
}
