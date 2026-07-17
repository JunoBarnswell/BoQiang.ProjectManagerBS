using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseAgentFlowEventBuilder
{
    internal object BuildAgentFlowEvent(string status, string traceId, string? error = null) =>
        new { status, traceId, error };

    internal object BuildNextAgentFlowEvent(FlowiseRuntimeNode node, string status) =>
        new
        {
            nodeId = node.Id,
            nodeLabel = NormalizeWorkflowDisplayText(node.DisplayName),
            status
        };

    internal object BuildHumanInputAction(FlowiseRuntimeNode node, string? question)
    {
        var message = ResolveHumanInputMessage(node, question);
        return new
        {
            id = Guid.NewGuid().ToString("N"),
            mapping = new
            {
                approve = "Proceed",
                reject = "Reject"
            },
            elements = new[]
            {
                new { type = "workflow-approve-button", label = "Proceed" },
                new { type = "workflow-reject-button", label = "Reject" }
            },
            data = new
            {
                nodeId = node.Id,
                nodeLabel = NormalizeWorkflowDisplayText(node.DisplayName),
                input = message
            }
        };
    }

    internal string ResolveHumanInputMessage(FlowiseRuntimeNode node, string? question) =>
        ResolveConfiguredHumanInputMessage(node, question);

    internal string NormalizeWorkflowDisplayText(string value) =>
        value
            .Replace("Start Agentflow", "Start", StringComparison.OrdinalIgnoreCase)
            .Replace("Agentflow V2", "Workflow", StringComparison.OrdinalIgnoreCase)
            .Replace("Agentflow v2", "Workflow", StringComparison.OrdinalIgnoreCase)
            .Replace("Agentflow", "Workflow", StringComparison.OrdinalIgnoreCase);

    private static string ResolveConfiguredHumanInputMessage(FlowiseRuntimeNode node, string? question)
    {
        var configuredMessage = ReadDataString(node.Data, "message") ??
            ReadNestedDataString(node.Data, "inputs", "message") ??
            ReadNestedDataString(node.Data, "config", "message");
        if (!string.IsNullOrWhiteSpace(configuredMessage))
        {
            return configuredMessage;
        }

        return string.IsNullOrWhiteSpace(question) ? "Please review and choose the next action." : question.Trim();
    }

    private static string? ReadDataString(IReadOnlyDictionary<string, JsonElement> data, string propertyName)
    {
        if (!data.TryGetValue(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static string? ReadNestedDataString(IReadOnlyDictionary<string, JsonElement> data, string parentPropertyName, string propertyName)
    {
        if (!data.TryGetValue(parentPropertyName, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }
}
