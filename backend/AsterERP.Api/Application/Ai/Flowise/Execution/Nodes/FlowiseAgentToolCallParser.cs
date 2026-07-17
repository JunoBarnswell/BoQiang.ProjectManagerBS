using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseAgentToolCallParser(
    FlowiseExecutionJsonDocumentParser jsonDocumentParser,
    FlowiseExecutionTemplateResolver templateResolver)
{
    internal IReadOnlyList<AgentToolCall> ReadToolCalls(
        IReadOnlyDictionary<string, JsonElement> data,
        FlowiseRuntimeNodeDataReader nodeDataReader,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, "agentTools", out var value))
        {
            return [];
        }

        var calls = new List<AgentToolCall>();
        using var document = jsonDocumentParser.ParseElementAsDocument(value);
        ReadToolCalls(document.RootElement, calls, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults);
        return calls
            .Where(item => !string.IsNullOrWhiteSpace(item.ToolCode))
            .ToList();
    }

    private void ReadToolCalls(
        JsonElement value,
        List<AgentToolCall> calls,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    ReadToolCalls(item, calls, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults);
                }
                break;
            case JsonValueKind.Object:
                var toolCode = FlowiseJsonElementReader.FirstNonEmpty(
                    FlowiseJsonElementReader.ReadString(value, "toolCode"),
                    FlowiseJsonElementReader.ReadString(value, "code"),
                    FlowiseJsonElementReader.ReadString(value, "name"),
                    FlowiseJsonElementReader.ReadString(value, "value"));
                if (!string.IsNullOrWhiteSpace(toolCode))
                {
                    calls.Add(new AgentToolCall(
                        toolCode,
                        ReadArgumentObject(value, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults)));
                }
                break;
            case JsonValueKind.String:
                var raw = value.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    calls.Add(new AgentToolCall(raw.Trim(), new Dictionary<string, object?>()));
                }
                break;
        }
    }

    private Dictionary<string, object?> ReadArgumentObject(
        JsonElement tool,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults)
    {
        var argumentsElement = FlowiseJsonElementReader.FirstExistingProperty(tool, "arguments", "args", "input", "parameters");
        if (argumentsElement is null && tool.TryGetProperty("argumentsJson", out var argumentsJson))
        {
            argumentsElement = argumentsJson;
        }

        if (argumentsElement is null)
        {
            return new Dictionary<string, object?>();
        }

        return ReadArgumentValueObject(argumentsElement.Value, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults);
    }

    private Dictionary<string, object?> ReadArgumentValueObject(
        JsonElement value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var raw = templateResolver.ResolveAgentTemplate(value.GetString() ?? string.Empty, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new Dictionary<string, object?>();
            }

            try
            {
                using var document = JsonDocument.Parse(raw);
                return ReadArgumentValueObject(document.RootElement, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults);
            }
            catch (JsonException)
            {
                return new Dictionary<string, object?> { ["input"] = raw };
            }
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, object?>();
        }

        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            arguments[property.Name] = ResolveArgumentValue(property.Value, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults);
        }

        return arguments;
    }

    private object? ResolveArgumentValue(
        JsonElement value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => templateResolver.ResolveAgentTemplate(value.GetString() ?? string.Empty, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults),
            JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => ReadArgumentValueObject(value, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults),
            JsonValueKind.Array => value.EnumerateArray()
                .Select(item => ResolveArgumentValue(item, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults))
                .ToList(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.GetRawText()
        };
    }
}
