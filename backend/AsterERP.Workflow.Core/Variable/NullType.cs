namespace AsterERP.Workflow.Core.Variable;

public class NullType : IVariableType
{
    public string TypeName => "null";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value == null;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.TextValue = null;
        variableInstance.TextValue2 = null;
        variableInstance.LongValue = null;
        variableInstance.DoubleValue = null;
        variableInstance.ByteValue = null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance) => null;

    public string? GetTypeForValue(object? value) => TypeName;
}
