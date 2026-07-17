using System;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Variable;

public class BigDecimalType : IVariableType
{
    public string TypeName => "bigdecimal";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is decimal;

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

        if (value is not decimal d)
        {
            throw new WorkflowEngineArgumentException("BigDecimalType only supports decimal values.");
        }

        variableInstance.TextValue = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        variableInstance.TextValue2 = null;
        variableInstance.LongValue = null;
        variableInstance.DoubleValue = null;
        variableInstance.ByteValue = null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.TextValue != null &&
            decimal.TryParse(variableInstance.TextValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        return null;
    }

    public string? GetTypeForValue(object? value)
    {
        return IsAbleToStore(value) ? TypeName : null;
    }
}
