using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static class ApplicationDataCenterJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string NormalizeObjectJson(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ValidationException($"{fieldName}必须是 JSON 对象", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            return document.RootElement.GetRawText();
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"{fieldName}不是合法 JSON: {ex.Message}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    public static Dictionary<string, object?> DeserializeDictionary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(value, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static T? Deserialize<T>(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(value, Options);
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
