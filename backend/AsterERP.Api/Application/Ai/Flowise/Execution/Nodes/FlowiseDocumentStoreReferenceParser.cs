using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseDocumentStoreReferenceParser(
    FlowiseExecutionJsonDocumentParser jsonDocumentParser)
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal IReadOnlyList<DocumentStoreReference> ReadReferences(IReadOnlyDictionary<string, JsonElement> data, FlowiseRuntimeNodeDataReader nodeDataReader)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, "agentKnowledgeDocumentStores", out var value))
        {
            return [];
        }

        var references = new List<DocumentStoreReference>();
        using var document = jsonDocumentParser.ParseElementAsDocument(value);
        ReadReferences(document.RootElement, references);
        return references
            .Where(item => !string.IsNullOrWhiteSpace(item.StoreId))
            .GroupBy(item => item.StoreId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    internal string MergeMetadata(string metadataJson, string storeId, string? storeName)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["storeId"] = storeId
        };
        if (!string.IsNullOrWhiteSpace(storeName))
        {
            metadata["storeName"] = storeName;
        }

        if (!string.IsNullOrWhiteSpace(metadataJson))
        {
            try
            {
                using var document = JsonDocument.Parse(metadataJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        metadata[property.Name] = JsonSerializer.Deserialize<object?>(property.Value.GetRawText(), CaseInsensitiveJsonOptions);
                    }
                }
            }
            catch (JsonException)
            {
                metadata["rawMetadata"] = metadataJson;
            }
        }

        return JsonSerializer.Serialize(metadata, CaseInsensitiveJsonOptions);
    }

    private void ReadReferences(JsonElement value, List<DocumentStoreReference> references)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    ReadReferences(item, references);
                }
                break;
            case JsonValueKind.Object:
                var storeId = FlowiseJsonElementReader.FirstNonEmpty(
                    FlowiseJsonElementReader.ReadString(value, "storeId"),
                    FlowiseJsonElementReader.ReadString(value, "documentStoreId"),
                    FlowiseJsonElementReader.ReadString(value, "id"),
                    FlowiseJsonElementReader.ReadString(value, "value"));
                if (!string.IsNullOrWhiteSpace(storeId))
                {
                    references.Add(new DocumentStoreReference(
                        storeId.Trim(),
                        FlowiseJsonElementReader.FirstNonEmpty(
                            FlowiseJsonElementReader.ReadString(value, "name"),
                            FlowiseJsonElementReader.ReadString(value, "label"),
                            FlowiseJsonElementReader.ReadString(value, "displayName")),
                        Math.Clamp(
                            FlowiseJsonElementReader.ReadInt(value, "topK") ?? FlowiseJsonElementReader.ReadInt(value, "limit") ?? 4,
                            1,
                            20)));
                }

                foreach (var property in value.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        ReadReferences(property.Value, references);
                    }
                }
                break;
            case JsonValueKind.String:
                var raw = value.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    break;
                }

                var trimmed = raw.Trim();
                if ((trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)) ||
                    (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal)))
                {
                    try
                    {
                        using var nested = JsonDocument.Parse(trimmed);
                        ReadReferences(nested.RootElement, references);
                    }
                    catch (JsonException)
                    {
                        references.Add(new DocumentStoreReference(trimmed, null, 4));
                    }
                }
                else
                {
                    references.Add(new DocumentStoreReference(trimmed, null, 4));
                }
                break;
        }
    }
}
