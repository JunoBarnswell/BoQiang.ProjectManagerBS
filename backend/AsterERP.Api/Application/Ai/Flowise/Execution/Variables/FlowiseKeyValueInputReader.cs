using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseKeyValueInputReader(
    FlowiseRuntimeNodeDataReader nodeDataReader,
    FlowiseVariableResolver variableResolver,
    FlowiseOutputReferenceResolver outputReferenceResolver)
{
    internal IReadOnlyList<KeyValuePair<string, string>> Read(
        IReadOnlyDictionary<string, JsonElement> data,
        string propertyName,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, propertyName, out var value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var document = JsonDocument.Parse(value.GetString() ?? "[]");
                return ReadArray(document.RootElement, context, runtimeModelResults, httpResults);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return ReadArray(value, context, runtimeModelResults, httpResults);
    }

    private IReadOnlyList<KeyValuePair<string, string>> ReadArray(
        JsonElement value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<KeyValuePair<string, string>>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = FlowiseJsonElementReader.ReadString(item, "key") ?? FlowiseJsonElementReader.ReadString(item, "Key");
            var rawValue = FlowiseJsonElementReader.ReadJsonPropertyAsString(item, "value");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var resolved = outputReferenceResolver.ReplaceHttpOutputReferences(
                variableResolver.ReplaceRuntimeVariables(rawValue ?? string.Empty, context, null, runtimeModelResults),
                httpResults);
            result.Add(new KeyValuePair<string, string>(key.Trim(), resolved));
        }

        return result;
    }
}
