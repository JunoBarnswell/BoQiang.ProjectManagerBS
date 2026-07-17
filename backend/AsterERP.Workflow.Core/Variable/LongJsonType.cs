using System;
using System.Text;

namespace AsterERP.Workflow.Core.Variable;

public class LongJsonType : IVariableType
{
    public string TypeName => "longJson";
    public bool IsCachable => false;

    public bool IsAbleToStore(object? value)
    {
        return JsonVariableSemantics.IsJsonText(value, out var json)
               && json.Length > JsonVariableSemantics.JsonTextMaxLength;
    }

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        if (JsonVariableSemantics.IsJsonText(value, out var json))
        {
            variableInstance.ByteValue = Encoding.UTF8.GetBytes(json);
            variableInstance.TextValue = null;
        }
        else
        {
            variableInstance.ByteValue = null;
            variableInstance.TextValue = null;
        }

        variableInstance.TextValue2 = null;
        variableInstance.LongValue = null;
        variableInstance.DoubleValue = null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.ByteValue != null)
        {
            return Encoding.UTF8.GetString(variableInstance.ByteValue);
        }

        return null;
    }

    public string? GetTypeForValue(object? value)
    {
        return IsAbleToStore(value) ? TypeName : null;
    }
}
