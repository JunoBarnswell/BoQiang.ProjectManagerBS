using System;
using System.Globalization;

namespace AsterERP.Workflow.Core.Variable;

public class LocalDateTimeType : IVariableType
{
    public string TypeName => "localDateTime";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is DateTimeOffset;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.TextValue = value is DateTimeOffset dto
            ? dto.ToString("o", CultureInfo.InvariantCulture)
            : null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.TextValue != null &&
            DateTimeOffset.TryParse(variableInstance.TextValue, null, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto;
        }

        return null;
    }

    public string? GetTypeForValue(object? value) => TypeName;
}
