using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

/// <summary>
/// Defines the one canonical representation used by document persistence and artifact integrity checks.
/// Object property order is normalized recursively; array order remains document semantics.
/// </summary>
public static class ApplicationDesignerCanonicalJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions RuntimeArtifactOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static string NormalizeObject(string json) => NormalizeObjectCore(json, Options);

    public static string NormalizeRuntimeObject(string json) => NormalizeObjectCore(json, RuntimeArtifactOptions);

    private static string NormalizeObjectCore(string json, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ValidationException("Designer Document cannot be empty", ErrorCodes.DesignerSchemaInvalid);
        }

        JsonNode parsed;
        try
        {
            parsed = JsonNode.Parse(json) ?? throw new InvalidOperationException("JSON is empty");
        }
        catch (JsonException exception)
        {
            throw new ValidationException($"Designer Document is invalid JSON: {exception.Message}", ErrorCodes.DesignerSchemaInvalid);
        }

        if (parsed is not JsonObject)
        {
            throw new ValidationException("Designer Document must be a JSON object", ErrorCodes.DesignerSchemaInvalid);
        }

        return NormalizeNode(parsed).ToJsonString(options);
    }

    public static string NormalizeDocument(string json)
    {
        var document = JsonNode.Parse(NormalizeObject(json)) as JsonObject
            ?? throw new ValidationException("Designer Document must be a JSON object", ErrorCodes.DesignerSchemaInvalid);
        document.Remove("documentHash");
        return NormalizeObject(document.ToJsonString(Options));
    }

    public static string ComputeHash(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    public static string ComputeDocumentHash(string documentJson) =>
        $"sha256:{ComputeHash(NormalizeDocument(documentJson))}";

    public static string ComputeSignature(params string[] values) =>
        ComputeHash(JsonSerializer.Serialize(string.Join("\n", values), RuntimeArtifactOptions));

    public static string ComputeRuntimeArtifactHash(JsonNode document)
    {
        var bytes = Encoding.UTF8.GetBytes(document.ToJsonString(RuntimeArtifactOptions));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }

    private static JsonNode NormalizeNode(JsonNode node)
    {
        if (node is JsonObject objectNode)
        {
            var normalized = new JsonObject();
            foreach (var property in objectNode.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                normalized[property.Key] = property.Value is null ? null : NormalizeNode(property.Value);
            }

            return normalized;
        }

        if (node is JsonArray arrayNode)
        {
            var normalized = new JsonArray();
            foreach (var item in arrayNode)
            {
                normalized.Add(item is null ? null : NormalizeNode(item));
            }

            return normalized;
        }

        return node.DeepClone();
    }
}
