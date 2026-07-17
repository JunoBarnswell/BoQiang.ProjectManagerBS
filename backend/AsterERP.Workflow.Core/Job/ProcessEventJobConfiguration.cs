using System.Text.Json;

namespace AsterERP.Workflow.Core.Job;

internal sealed class ProcessEventJobConfiguration
{
    public string? EventName { get; private init; }
    public string? MessageName { get; private init; }
    public string? SignalName { get; private init; }
    public string? ExecutionId { get; private init; }
    public Dictionary<string, object?> Payload { get; private init; } = new();

    public static ProcessEventJobConfiguration Parse(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
            return new ProcessEventJobConfiguration();

        try
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configuration);
            if (json == null)
                return new ProcessEventJobConfiguration { EventName = configuration };

            return new ProcessEventJobConfiguration
            {
                EventName = GetString(json, "eventName") ?? GetString(json, "eventType"),
                MessageName = GetString(json, "messageName"),
                SignalName = GetString(json, "signalName"),
                ExecutionId = GetString(json, "executionId"),
                Payload = GetPayload(json)
            };
        }
        catch
        {
            return new ProcessEventJobConfiguration { EventName = configuration };
        }
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> json, string key)
    {
        return json.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static Dictionary<string, object?> GetPayload(IReadOnlyDictionary<string, JsonElement> json)
    {
        if (!json.TryGetValue("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, object?>();

        var result = new Dictionary<string, object?>();
        foreach (var property in payload.EnumerateObject())
            result[property.Name] = JsonElementToObject(property.Value);

        return result;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number when element.TryGetInt64(out var longValue):
                return longValue;
            case JsonValueKind.Number when element.TryGetDouble(out var doubleValue):
                return doubleValue;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            default:
                return element.GetRawText();
        }
    }
}
