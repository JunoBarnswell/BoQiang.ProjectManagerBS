using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseRuntimeNodeDataReader
{
    internal string? ReadNodeInputString(IReadOnlyDictionary<string, JsonElement> data, string propertyName) =>
        TryGetNodeInputValue(data, propertyName, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
            : null;

    internal int? ReadNodeInputInt(IReadOnlyDictionary<string, JsonElement> data, string propertyName)
    {
        if (!TryGetNodeInputValue(data, propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    internal bool ReadNodeInputBool(IReadOnlyDictionary<string, JsonElement> data, string propertyName)
    {
        if (!TryGetNodeInputValue(data, propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var flag) && flag,
            _ => false
        };
    }

    internal bool TryGetNodeInputValue(IReadOnlyDictionary<string, JsonElement> data, string propertyName, out JsonElement value)
    {
        if (data.TryGetValue(propertyName, out value))
        {
            return true;
        }

        foreach (var containerName in new[] { "inputs", "config" })
        {
            if (data.TryGetValue(containerName, out var container) &&
                container.ValueKind == JsonValueKind.Object &&
                container.TryGetProperty(propertyName, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    internal string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    internal string? ReadDataString(IReadOnlyDictionary<string, JsonElement> data, string propertyName)
    {
        if (!data.TryGetValue(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    internal string? ReadNestedDataString(IReadOnlyDictionary<string, JsonElement> data, string parentPropertyName, string propertyName)
    {
        if (!data.TryGetValue(parentPropertyName, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    internal int? ReadDataInt(IReadOnlyDictionary<string, JsonElement> data, string propertyName)
    {
        if (!data.TryGetValue(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    internal string ReadJsonPropertyAsString(JsonElement element, string propertyName)
    {
        if (!TryGetJsonProperty(element, propertyName, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => value.GetRawText()
        };
    }

    internal bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
