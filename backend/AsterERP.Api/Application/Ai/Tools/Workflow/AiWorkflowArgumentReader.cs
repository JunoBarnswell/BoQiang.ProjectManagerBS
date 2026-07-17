using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public static class AiWorkflowArgumentReader
{
    public static string? ReadString(IReadOnlyDictionary<string, object?> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString()?.Trim(),
            JsonElement element => element.ToString(),
            _ => value.ToString()?.Trim()
        };
    }

    public static int ReadInt(IReadOnlyDictionary<string, object?> arguments, string name, int fallback)
    {
        var text = ReadString(arguments, name);
        return int.TryParse(text, out var value) ? value : fallback;
    }

    public static Dictionary<string, object?> ReadObject(
        IReadOnlyDictionary<string, object?> arguments,
        string name,
        Dictionary<string, object?>? fallback = null)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return fallback ?? [];
        }

        if (value is Dictionary<string, object?> dictionary)
        {
            return new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), WorkflowJsonOptions.Options)
                   ?? fallback
                   ?? [];
        }

        if (value is string text && text.TrimStart().StartsWith('{'))
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(text, WorkflowJsonOptions.Options)
                   ?? fallback
                   ?? [];
        }

        return fallback ?? [];
    }

    public static IReadOnlyList<string> ReadStringList(IReadOnlyDictionary<string, object?> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return [];
        }

        if (value is IEnumerable<string> strings)
        {
            return strings.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToList();
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<string>>(element.GetRawText(), WorkflowJsonOptions.Options) ?? [];
        }

        return ReadString(arguments, name)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    }
}
