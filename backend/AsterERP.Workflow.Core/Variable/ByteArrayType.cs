namespace AsterERP.Workflow.Core.Variable;

public class ByteArrayType : IVariableType
{
    public string TypeName => "bytes";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is null || value is byte[];

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.ByteValue = value as byte[];
        variableInstance.TextValue = null;
        variableInstance.TextValue2 = null;
        variableInstance.LongValue = null;
        variableInstance.DoubleValue = null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance) => variableInstance.ByteValue;

    public string? GetTypeForValue(object? value) => IsAbleToStore(value) ? TypeName : null;
}
