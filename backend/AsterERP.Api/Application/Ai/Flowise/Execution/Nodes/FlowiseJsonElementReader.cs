using System.Globalization;
using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

internal static class FlowiseJsonElementReader
{
    internal static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    internal static string? ReadJsonPropertyAsString(JsonElement element, string propertyName)
    {
        if (!TryGetJsonProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    internal static bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement value)
    {
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

    internal static JsonElement? FirstExistingProperty(JsonElement value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (value.TryGetProperty(propertyName, out var property))
            {
                return property;
            }
        }

        return null;
    }

    internal static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))?.Trim();

    internal static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numeric))
        {
            return numeric;
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    internal static string NormalizeJsonElement(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? "[]" : text;
        }

        return value.GetRawText();
    }
}
