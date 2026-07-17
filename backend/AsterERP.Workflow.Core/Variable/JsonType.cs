namespace AsterERP.Workflow.Core.Variable;

public class JsonType : IVariableType
{
    public string TypeName => "json";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value)
    {
        if (!JsonVariableSemantics.IsJsonText(value, out var json))
        {
            return false;
        }

        return json.Length <= JsonVariableSemantics.JsonTextMaxLength;
    }

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.TextValue = JsonVariableSemantics.IsJsonText(value, out var json) ? json : null;
        variableInstance.TextValue2 = null;
        variableInstance.ByteValue = null;
        variableInstance.LongValue = null;
        variableInstance.DoubleValue = null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance) => variableInstance.TextValue;

    public string? GetTypeForValue(object? value)
    {
        return IsAbleToStore(value) ? TypeName : null;
    }
}
