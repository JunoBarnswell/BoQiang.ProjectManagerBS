using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionArgumentRedactor
{
    private const string RedactedValue = "***REDACTED***";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] DefaultSensitiveNames =
    [
        "password",
        "newPassword",
        "token",
        "accessToken",
        "refreshToken",
        "secret",
        "apiKey",
        "authorization",
        "headers"
    ];

    public string RedactJson(AiKernelFunctionDefinition definition, string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var sensitiveNames = BuildSensitiveNames(definition);
            var redacted = RedactElement(document.RootElement, null, sensitiveNames);
            return JsonSerializer.Serialize(redacted, JsonOptions);
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    private static HashSet<string> BuildSensitiveNames(AiKernelFunctionDefinition definition)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in DefaultSensitiveNames)
        {
            names.Add(name);
        }

        foreach (var name in definition.SensitiveArgumentNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name.Trim());
            }
        }

        return names;
    }

    private static object? RedactElement(JsonElement element, string? currentName, HashSet<string> sensitiveNames)
    {
        if (IsSensitiveName(currentName, sensitiveNames))
        {
            return RedactedValue;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => RedactObject(element, sensitiveNames),
            JsonValueKind.Array => element.EnumerateArray().Select(item => RedactElement(item, currentName, sensitiveNames)).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, object?> RedactObject(JsonElement element, HashSet<string> sensitiveNames)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = RedactElement(property.Value, property.Name, sensitiveNames);
        }

        return result;
    }

    private static bool IsSensitiveName(string? name, HashSet<string> sensitiveNames)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.Trim();
        if (sensitiveNames.Contains(normalized))
        {
            return true;
        }

        var compact = normalized.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return compact.Contains("password", StringComparison.Ordinal) ||
               compact.Contains("token", StringComparison.Ordinal) ||
               compact.Contains("secret", StringComparison.Ordinal) ||
               compact.Contains("apikey", StringComparison.Ordinal) ||
               compact.Contains("authorization", StringComparison.Ordinal);
    }
}
