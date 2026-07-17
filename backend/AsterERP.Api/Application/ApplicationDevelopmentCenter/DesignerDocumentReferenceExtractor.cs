using System.Text.Json;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

/// <summary>
/// Extracts object identities from declared latest DesignerDocument fields. Labels,
/// metadata and unknown extension values are intentionally not object references.
/// </summary>
public sealed class DesignerDocumentReferenceExtractor
{
    private static readonly HashSet<string> ReferenceFieldNames = new(StringComparer.Ordinal)
    {
        "resourceId", "dataSourceId", "sourceObjectId", "targetObjectId", "modelId",
        "entityId", "queryId", "apiServiceId", "microflowId", "workflowId"
    };

    private static readonly string[] RootContractFields =
    ["dataSources", "apiBindings", "pageMicroflows", "pageParameters", "variables", "workflowBindings"];

    private static readonly string[] NodeContractFields =
    ["bindings", "props", "layout", "style", "responsiveOverrides", "events", "permission", "validation"];

    public bool References(string documentJson, string targetObjectId)
    {
        if (string.IsNullOrWhiteSpace(documentJson) || string.IsNullOrWhiteSpace(targetObjectId)) return false;
        try
        {
            using var document = JsonDocument.Parse(documentJson);
            return Extract(document.RootElement).Contains(targetObjectId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public IReadOnlySet<string> Extract(JsonElement document)
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (document.ValueKind != JsonValueKind.Object) return references;

        foreach (var field in RootContractFields)
        {
            if (document.TryGetProperty(field, out var value)) CollectReferences(value, references);
        }

        if (document.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Object)
        {
            foreach (var element in elements.EnumerateObject())
            {
                if (element.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (var field in NodeContractFields)
                {
                    if (element.Value.TryGetProperty(field, out var value)) CollectReferences(value, references);
                }
            }
        }

        return references;
    }

    private static void CollectReferences(JsonElement value, ISet<string> references)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) CollectReferences(item, references);
            return;
        }

        if (value.ValueKind != JsonValueKind.Object) return;
        foreach (var property in value.EnumerateObject())
        {
            if (ReferenceFieldNames.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.String)
            {
                var identifier = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(identifier)) references.Add(identifier.Trim());
            }

            CollectReferences(property.Value, references);
        }
    }
}
