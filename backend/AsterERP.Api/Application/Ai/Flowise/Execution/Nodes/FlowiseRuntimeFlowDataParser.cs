using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseRuntimeFlowDataParser(FlowiseRuntimeNodeDataReader nodeDataReader)
{
    internal FlowiseRuntimeFlowData Parse(string flowData)
    {
        using var document = JsonDocument.Parse(NormalizeJson(flowData));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ValidationException("Flowise flowData 必须是 JSON object", ErrorCodes.ParameterInvalid);
        }

        var nodes = document.RootElement.TryGetProperty("nodes", out var nodeArray) && nodeArray.ValueKind == JsonValueKind.Array
            ? nodeArray.EnumerateArray().Select(MapNode).Where(item => !string.IsNullOrWhiteSpace(item.Id)).ToList()
            : [];
        var edges = document.RootElement.TryGetProperty("edges", out var edgeArray) && edgeArray.ValueKind == JsonValueKind.Array
            ? edgeArray.EnumerateArray().Select(MapEdge).Where(item => !string.IsNullOrWhiteSpace(item.Id)).ToList()
            : [];

        return new FlowiseRuntimeFlowData { Nodes = nodes, Edges = edges };
    }

    private FlowiseRuntimeNode MapNode(JsonElement element)
    {
        var data = element.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataElement.GetRawText()) ?? []
            : [];

        return new FlowiseRuntimeNode
        {
            Id = nodeDataReader.ReadString(element, "id") ?? string.Empty,
            Data = data,
            DisplayName = nodeDataReader.ReadDataString(data, "displayName") ??
                nodeDataReader.ReadDataString(data, "label") ??
                nodeDataReader.ReadString(element, "id") ??
                string.Empty,
            NodeType = nodeDataReader.ReadDataString(data, "nodeType") ??
                nodeDataReader.ReadDataString(data, "name") ??
                nodeDataReader.ReadString(element, "type") ??
                string.Empty,
            ParentId = nodeDataReader.ReadString(element, "parentId") ?? nodeDataReader.ReadString(element, "parentNode")
        };
    }

    private FlowiseRuntimeEdge MapEdge(JsonElement element) => new()
    {
        Id = nodeDataReader.ReadString(element, "id") ?? string.Empty,
        Source = nodeDataReader.ReadString(element, "source") ?? string.Empty,
        SourceHandle = nodeDataReader.ReadString(element, "sourceHandle"),
        Target = nodeDataReader.ReadString(element, "target") ?? string.Empty,
        TargetHandle = nodeDataReader.ReadString(element, "targetHandle")
    };

    private static string NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "{}";
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return value.Trim();
        }
        catch (JsonException)
        {
            throw new ValidationException("JSON 格式不正确", ErrorCodes.ParameterInvalid);
        }
    }
}
