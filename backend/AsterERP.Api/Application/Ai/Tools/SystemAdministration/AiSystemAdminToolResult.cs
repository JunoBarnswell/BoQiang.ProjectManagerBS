using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Tools.SystemAdministration;

public static class AiSystemAdminToolResult
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AiKernelFunctionResult Succeeded(
        AiKernelFunctionContext context,
        string module,
        string action,
        object? item = null,
        object? page = null,
        IReadOnlyList<string>? affectedIds = null,
        IReadOnlyList<string>? warnings = null)
    {
        var payload = new
        {
            module,
            action,
            status = "Succeeded",
            affectedIds = affectedIds ?? [],
            item,
            page,
            warnings = warnings ?? [],
            workspace = new
            {
                tenantId = context.TenantId,
                appCode = context.AppCode
            }
        };
        var json = RedactSensitiveOutput(JsonSerializer.Serialize(payload, JsonOptions));
        return new AiKernelFunctionResult
        {
            ResultSummary = $"{module}.{action} 执行成功",
            Content = json,
            EvidenceJson = json,
            OutputType = "Json"
        };
    }

    private static string RedactSensitiveOutput(string json)
    {
        using var document = JsonDocument.Parse(json);
        var redacted = RedactElement(document.RootElement, null);
        return JsonSerializer.Serialize(redacted, JsonOptions);
    }

    private static object? RedactElement(JsonElement element, string? propertyName)
    {
        if (IsSensitive(propertyName))
        {
            return "***REDACTED***";
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => RedactObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(item => RedactElement(item, propertyName)).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, object?> RedactObject(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = RedactElement(property.Value, property.Name);
        }

        return result;
    }

    private static bool IsSensitive(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var compact = propertyName.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return compact.Contains("password", StringComparison.Ordinal) ||
               compact.Contains("token", StringComparison.Ordinal) ||
               compact.Contains("secret", StringComparison.Ordinal) ||
               compact.Contains("apikey", StringComparison.Ordinal) ||
               compact.Contains("authorization", StringComparison.Ordinal) ||
               string.Equals(compact, "headers", StringComparison.Ordinal);
    }
}
