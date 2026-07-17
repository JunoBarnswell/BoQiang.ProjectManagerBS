using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Flowise;

internal static class FlowiseResourceJson
{
    public static string NormalizeObject(string? json, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ValidationException($"{fieldName} 必须是 JSON object", ErrorCodes.ParameterInvalid);
            }

            return document.RootElement.GetRawText();
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"{fieldName} 不是有效 JSON: {ex.Message}", ErrorCodes.ParameterInvalid);
        }
    }

    public static string NormalizeStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Enabled" : status.Trim();
        return normalized is "Enabled" or "Disabled" or "Archived"
            ? normalized
            : throw new ValidationException("Flowise 状态必须是 Enabled、Disabled 或 Archived", ErrorCodes.ParameterInvalid);
    }

    public static string Required(string? value, string fieldName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ValidationException($"{fieldName} 必填", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    public static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string NewApiKey()
    {
        return $"flw_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";
    }
}
