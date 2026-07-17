namespace AsterERP.Workflow.Core.Variable;

public class LongType : IVariableType
{
    public string TypeName => "long";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is long;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.LongValue = value as long?;
    }

    public object? GetValue(VariableInstanceEntity variableInstance) => variableInstance.LongValue;

    public string? GetTypeForValue(object? value) => TypeName;
}
