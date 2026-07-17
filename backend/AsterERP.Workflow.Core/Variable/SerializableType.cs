using System.Text.Json;

namespace AsterERP.Workflow.Core.Variable;

public class SerializableType : IVariableType
{
    public string TypeName => "serializable";
    public bool IsCachable => false;

    public bool IsAbleToStore(object? value) => value != null;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.TextValue = value?.GetType().AssemblyQualifiedName;
        variableInstance.TextValue2 = JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object));
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.TextValue2 != null && variableInstance.TextValue != null)
        {
            var type = Type.GetType(variableInstance.TextValue);
            if (type != null)
            {
                try
                {
                    return JsonSerializer.Deserialize(variableInstance.TextValue2, type);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    public string? GetTypeForValue(object? value) => TypeName;
}
