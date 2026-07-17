namespace AsterERP.Workflow.Core.Variable;

public class StringType : IVariableType
{
    private const int MaxLength = 4000;

    public string TypeName => "string";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is string s && s.Length < MaxLength;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.TextValue = value as string;
        variableInstance.TextValue2 = null;
        variableInstance.LongValue = null;
        variableInstance.DoubleValue = null;
        variableInstance.ByteValue = null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance) => variableInstance.TextValue;

    public string? GetTypeForValue(object? value)
    {
        return IsAbleToStore(value) ? TypeName : null;
    }
}
