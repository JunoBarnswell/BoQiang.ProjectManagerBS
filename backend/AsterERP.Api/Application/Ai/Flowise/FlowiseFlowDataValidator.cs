using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseFlowDataValidator
{
    public FlowiseCanvasValidationResultDto Validate(string? flowData)
    {
        var issues = new List<FlowiseCanvasValidationIssueDto>();
        ValidateFlowData(flowData, issues);
        return new FlowiseCanvasValidationResultDto
        {
            Valid = issues.All(issue => issue.Severity != "error"),
            Issues = issues
        };
    }

    public string Normalize(string? flowData)
    {
        if (string.IsNullOrWhiteSpace(flowData))
        {
            return """{"nodes":[],"edges":[],"viewport":{"x":0,"y":0,"zoom":1}}""";
        }

        using var document = JsonDocument.Parse(flowData);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ValidationException("Flowise flowData 必须是 JSON object", ErrorCodes.ParameterInvalid);
        }

        return flowData.Trim();
    }

    public string NormalizeJsonObject(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "{}";
        }

        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ValidationException("Flowise 配置字段必须是 JSON object", ErrorCodes.ParameterInvalid);
        }

        return value.Trim();
    }

    private static void ValidateFlowData(string? value, ICollection<FlowiseCanvasValidationIssueDto> issues)
    {
        try
        {
            var json = string.IsNullOrWhiteSpace(value)
                ? """{"nodes":[],"edges":[],"viewport":{"x":0,"y":0,"zoom":1}}"""
                : value.Trim();
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("invalid_flow_data", "Flowise flowData 必须是 JSON object"));
                return;
            }

            if (!document.RootElement.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            {
                issues.Add(Error("missing_flow_nodes", "Flowise flowData 缺少 nodes 数组"));
                return;
            }

            if (!document.RootElement.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
            {
                issues.Add(Error("missing_flow_edges", "Flowise flowData 缺少 edges 数组"));
                return;
            }

            var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connectedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in nodes.EnumerateArray())
            {
                var nodeId = ReadString(node, "id");
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    issues.Add(Error("invalid_flow_node", "Flowise node 缺少 id"));
                    continue;
                }

                if (!nodeIds.Add(nodeId))
                {
                    issues.Add(Error("duplicate_flow_node", $"Flowise node id 重复：{nodeId}", nodeId));
                }

                if (!node.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(Error("invalid_flow_node_data", $"Flowise node {nodeId} 缺少 data object", nodeId));
                    continue;
                }

                AddRequiredInputIssues(nodeId, data, issues);
            }

            var edgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in edges.EnumerateArray())
            {
                var edgeId = ReadString(edge, "id");
                var source = ReadString(edge, "source");
                var target = ReadString(edge, "target");
                if (string.IsNullOrWhiteSpace(edgeId) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                {
                    issues.Add(Error("invalid_flow_edge", "Flowise edge 缺少 id/source/target", null, edgeId));
                    continue;
                }

                if (!edgeIds.Add(edgeId))
                {
                    issues.Add(Error("duplicate_flow_edge", $"Flowise edge id 重复：{edgeId}", null, edgeId));
                }

                if (!nodeIds.Contains(source) || !nodeIds.Contains(target))
                {
                    issues.Add(Error("dangling_flow_edge", $"Flowise edge {edgeId} 引用了不存在的 node", null, edgeId));
                }
                else
                {
                    connectedNodeIds.Add(source);
                    connectedNodeIds.Add(target);
                }
            }

            foreach (var node in nodes.EnumerateArray())
            {
                var nodeId = ReadString(node, "id");
                if (string.IsNullOrWhiteSpace(nodeId) || connectedNodeIds.Contains(nodeId))
                {
                    continue;
                }

                if (node.TryGetProperty("data", out var data) && IsStickyNote(data))
                {
                    continue;
                }

                issues.Add(Warning("disconnected_flow_node", $"Flowise node {nodeId} 未连接到任何节点", nodeId));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(Error("invalid_flow_data_json", $"Flowise flowData 不是有效 JSON：{ex.Message}"));
        }
    }

    private static void AddRequiredInputIssues(string nodeId, JsonElement data, ICollection<FlowiseCanvasValidationIssueDto> issues)
    {
        if (!data.TryGetProperty("inputParams", out var inputParams) || inputParams.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        if (!data.TryGetProperty("inputs", out var inputs) || inputs.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var param in inputParams.EnumerateArray())
        {
            if (param.ValueKind != JsonValueKind.Object || IsOptional(param) || !ShouldShow(param, inputs) || ShouldHide(param, inputs))
            {
                continue;
            }

            var name = ReadString(param, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!inputs.TryGetProperty(name, out var inputValue) || IsEmptyInputValue(inputValue))
            {
                var label = ReadString(param, "label") ?? name;
                issues.Add(Warning("missing_required_node_input", $"Flowise node {nodeId} 缺少必填参数：{label}", nodeId));
            }
        }
    }

    private static bool ShouldShow(JsonElement param, JsonElement inputs)
    {
        if (!param.TryGetProperty("show", out var show) || show.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        foreach (var condition in show.EnumerateObject())
        {
            if (!inputs.TryGetProperty(condition.Name, out var actual) || !JsonValuesEqual(actual, condition.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldHide(JsonElement param, JsonElement inputs)
    {
        if (!param.TryGetProperty("hide", out var hide) || hide.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var condition in hide.EnumerateObject())
        {
            if (!inputs.TryGetProperty(condition.Name, out var actual) || !JsonValuesEqual(actual, condition.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool JsonValuesEqual(JsonElement actual, JsonElement expected)
    {
        if (expected.ValueKind == JsonValueKind.Array)
        {
            return expected.EnumerateArray().Any(item => JsonValuesEqual(actual, item));
        }

        return actual.ValueKind == expected.ValueKind
            ? string.Equals(actual.ToString(), expected.ToString(), StringComparison.Ordinal)
            : string.Equals(actual.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOptional(JsonElement param) =>
        param.TryGetProperty("optional", out var optional) &&
        optional.ValueKind is JsonValueKind.True;

    private static bool IsEmptyInputValue(JsonElement value) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
        value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()) ||
        value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0;

    private static bool IsStickyNote(JsonElement data)
    {
        var name = ReadString(data, "name");
        return string.Equals(name, "stickyNoteAgentflow", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "stickyNote", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static FlowiseCanvasValidationIssueDto Error(string code, string message, string? nodeId = null, string? edgeId = null) => new()
    {
        Code = code,
        EdgeId = edgeId,
        Message = message,
        NodeId = nodeId,
        Severity = "error"
    };

    private static FlowiseCanvasValidationIssueDto Warning(string code, string message, string? nodeId = null, string? edgeId = null) => new()
    {
        Code = code,
        EdgeId = edgeId,
        Message = message,
        NodeId = nodeId,
        Severity = "warning"
    };
}
