using System.Text.Json;

namespace AsterERP.Workflow.Core.Variable;

public class CustomObjectType : IVariableType
{
    public string TypeName => "customObject";
    public bool IsCachable => false;

    public bool IsAbleToStore(object? value)
    {
        if (value == null) return false;
        var type = value.GetType();
        return !type.IsPrimitive &&
               type != typeof(string) &&
               type != typeof(decimal) &&
               type != typeof(DateTime) &&
               type != typeof(byte[]);
    }

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        if (value == null)
        {
            variableInstance.TextValue = null;
            variableInstance.TextValue2 = null;
            return;
        }

        var type = value.GetType();
        variableInstance.TextValue = type.AssemblyQualifiedName;
        variableInstance.TextValue2 = JsonSerializer.Serialize(value, type);
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.TextValue == null || variableInstance.TextValue2 == null)
        {
            return null;
        }

        var type = Type.GetType(variableInstance.TextValue);
        if (type == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(variableInstance.TextValue2, type);
        }
        catch
        {
            return null;
        }
    }

    public string? GetTypeForValue(object? value) => TypeName;
}
