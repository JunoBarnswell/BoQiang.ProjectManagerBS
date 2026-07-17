using System.Text.Json;

namespace AsterERP.Api.Application.Workflows;

public static class WorkflowJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(object? value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    public static Dictionary<string, object?> DeserializeVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, Options) ?? [];
    }
}
