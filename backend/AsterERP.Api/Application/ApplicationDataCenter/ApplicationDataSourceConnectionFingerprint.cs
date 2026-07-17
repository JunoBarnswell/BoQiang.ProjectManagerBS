using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Modules.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static class ApplicationDataSourceConnectionFingerprint
{
    public static string Compute(ApplicationDataSourceEntity entity)
    {
        var payload = string.Join("\n", [
            entity.ObjectType.Trim(),
            entity.Endpoint?.Trim() ?? string.Empty,
            CanonicalJson(entity.ConfigJson),
            entity.SecretRef?.Trim() ?? string.Empty,
            Hash(entity.SecretConfigCipherText)
        ]);

        return Hash(payload);
    }

    private static string CanonicalJson(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return CanonicalElement(document.RootElement);
    }

    private static string CanonicalElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => JsonSerializer.Serialize(item.Name) + ":" + CanonicalElement(item.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(CanonicalElement)) + "]",
        _ => element.GetRawText()
    };

    private static string Hash(string? value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();
}
