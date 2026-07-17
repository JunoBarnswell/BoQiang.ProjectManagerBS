using System;
using System.Globalization;

namespace AsterERP.Workflow.Core.Variable;

public class LocalDateType : IVariableType
{
    public string TypeName => "localDate";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is DateOnly;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.TextValue = value is DateOnly d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.TextValue != null &&
            DateOnly.TryParseExact(variableInstance.TextValue, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d))
        {
            return d;
        }

        return null;
    }

    public string? GetTypeForValue(object? value) => TypeName;
}
