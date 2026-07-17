namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseStructuredOutputBuilder(
    FlowiseExecutionContentParser executionContentParser)
{
    internal IReadOnlyDictionary<string, object?> BuildLlmOutput(FlowiseRuntimeNode node, string content)
    {
        var fields = executionContentParser.ReadLlmStructuredOutputFields(node.Data);
        return BuildOutput(fields, content);
    }

    internal IReadOnlyDictionary<string, object?> BuildAgentOutput(FlowiseRuntimeNode node, string content)
    {
        var fields = executionContentParser.ReadAgentStructuredOutputFields(node.Data);
        return BuildOutput(fields, content);
    }

    private IReadOnlyDictionary<string, object?> BuildOutput(IReadOnlyList<string> fields, string content)
    {
        if (fields.Count == 0)
        {
            return new Dictionary<string, object?>();
        }

        if (executionContentParser.TryParseJsonObject(content, out var parsed))
        {
            return parsed;
        }

        return fields.ToDictionary(field => field, _ => (object?)content, StringComparer.OrdinalIgnoreCase);
    }
}
