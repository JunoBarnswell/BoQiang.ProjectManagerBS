using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public static class AiDataCenterArgumentReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string RequiredString(IReadOnlyDictionary<string, object?> arguments, string name)
    {
        var value = ReadString(arguments, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"缺少参数：{name}", ErrorCodes.ParameterInvalid);
        }

        return value;
    }

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

    public static int? ReadNullableInt(IReadOnlyDictionary<string, object?> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = ReadString(arguments, name);
        return int.TryParse(text, out var parsed)
            ? parsed
            : throw new ValidationException($"参数必须是整数：{name}", ErrorCodes.ParameterInvalid);
    }

    public static bool ReadBool(IReadOnlyDictionary<string, object?> arguments, string name, bool fallback)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            bool flag => flag,
            JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            JsonElement element when element.ValueKind == JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) ? parsed : fallback,
            string text => bool.TryParse(text, out var parsed) ? parsed : fallback,
            _ => fallback
        };
    }

    public static Dictionary<string, object?> ReadDictionary(IReadOnlyDictionary<string, object?> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), JsonOptions)
                   ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        if (value is string text && text.TrimStart().StartsWith('{'))
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(text, JsonOptions)
                   ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public static T ReadRequest<T>(IReadOnlyDictionary<string, object?> arguments, string name) where T : class
    {
        return TryReadRequest<T>(arguments, name)
               ?? throw new ValidationException($"缺少参数：{name}", ErrorCodes.ParameterInvalid);
    }

    public static T? TryReadRequest<T>(IReadOnlyDictionary<string, object?> arguments, string name) where T : class
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions);
        }

        if (value is string text && text.TrimStart().StartsWith('{'))
        {
            return JsonSerializer.Deserialize<T>(text, JsonOptions);
        }

        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
