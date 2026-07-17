namespace AsterERP.Workflow.Core.Variable;

public class DoubleType : IVariableType
{
    public string TypeName => "double";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is double or float;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.DoubleValue = value switch
        {
            double d => d,
            float f => f,
            _ => null
        };
    }

    public object? GetValue(VariableInstanceEntity variableInstance) => variableInstance.DoubleValue;

    public string? GetTypeForValue(object? value) => TypeName;
}
