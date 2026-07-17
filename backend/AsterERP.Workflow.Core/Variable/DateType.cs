using System.Globalization;

namespace AsterERP.Workflow.Core.Variable;

public class DateType : IVariableType
{
    public string TypeName => "date";
    public bool IsCachable => true;

    public bool IsAbleToStore(object? value) => value is DateTime;

    public void SetValue(object? value, VariableInstanceEntity variableInstance)
    {
        variableInstance.TextValue = value is DateTime dt ? dt.ToString("o") : null;
    }

    public object? GetValue(VariableInstanceEntity variableInstance)
    {
        if (variableInstance.TextValue != null &&
            DateTime.TryParse(variableInstance.TextValue, null, DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt;
        }

        return null;
    }

    public string? GetTypeForValue(object? value) => TypeName;
}
