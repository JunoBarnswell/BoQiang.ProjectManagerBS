using System.Text.Json;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public static class WorkflowVariableValueConverter
{
    public static Dictionary<string, object?>? Normalize(Dictionary<string, object>? variables)
    {
        return variables == null || variables.Count == 0
            ? null
            : variables.ToDictionary(kvp => kvp.Key, kvp => NormalizeValue(kvp.Value));
    }

    private static object? NormalizeValue(object? value)
    {
        return value is JsonElement jsonElement
            ? NormalizeJsonElement(jsonElement)
            : value;
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => NormalizeNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => NormalizeJsonElement(property.Value)),
            _ => element.ToString()
        };
    }

    private static object NormalizeNumber(JsonElement element)
    {
        if (element.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (element.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        return element.GetDecimal();
    }
}
