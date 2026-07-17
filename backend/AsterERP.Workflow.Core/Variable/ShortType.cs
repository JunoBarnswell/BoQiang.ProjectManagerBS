namespace AsterERP.Workflow.Core.Variable;

public class ShortType : IVariableType
{
    public string TypeName => "short";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is short;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.LongValue = value is short s ? s : null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        return variableInstance.LongValue.HasValue ? (short)variableInstance.LongValue.Value : null;
    }

    public string? GetTypeForValue(object? value) => TypeName;
}
