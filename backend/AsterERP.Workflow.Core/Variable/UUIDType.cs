using System;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Variable;

public class UUIDType : IVariableType
{
    public string TypeName => "uuid";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is Guid;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        if (value is null)
        {
            variableInstance.TextValue = null;
            variableInstance.TextValue2 = null;
            variableInstance.LongValue = null;
            variableInstance.DoubleValue = null;
            variableInstance.ByteValue = null;
            return;
        }

        if (value is not Guid g)
        {
            throw new WorkflowEngineArgumentException("UUIDType only supports Guid values.");
        }

        variableInstance.TextValue = g.ToString("D");
        variableInstance.TextValue2 = null;
        variableInstance.LongValue = null;
        variableInstance.DoubleValue = null;
        variableInstance.ByteValue = null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.TextValue != null && Guid.TryParseExact(variableInstance.TextValue, "D", out var g))
        {
            return g;
        }

        return null;
    }

    public string? GetTypeForValue(object? value)
    {
        return IsAbleToStore(value) ? TypeName : null;
    }
}
