using System;
using System.Text;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Variable;

public class LongStringType : IVariableType
{
    public string TypeName => "longString";
    public bool IsCachable => false;

    public bool IsAbleToStore(object? value) => value is string s && s.Length >= 4000;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        if (value is null)
        {
            variableInstance.ByteValue = null;
            variableInstance.TextValue = null;
            variableInstance.TextValue2 = null;
            variableInstance.LongValue = null;
            variableInstance.DoubleValue = null;
            return;
        }

        if (value is string s)
        {
            variableInstance.ByteValue = Encoding.UTF8.GetBytes(s);
            variableInstance.TextValue = null;
            variableInstance.TextValue2 = null;
            variableInstance.LongValue = null;
            variableInstance.DoubleValue = null;
        }
        else
        {
            throw new WorkflowEngineArgumentException("LongStringType only supports string values.");
        }
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.ByteValue != null)
        {
            return Encoding.UTF8.GetString(variableInstance.ByteValue);
        }

        return variableInstance.TextValue;
    }

    public string? GetTypeForValue(object? value)
    {
        return IsAbleToStore(value) ? TypeName : null;
    }
}
