using System.Text.Json;
using System.Text.RegularExpressions;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed partial class FlowiseOutputReferenceResolver
{
    internal string ReplaceHttpOutputReferences(string value, IReadOnlyList<HttpNodeResult> previousResults) =>
        ReplaceOutputReferences(
            value,
            previousResults,
            result => result.NodeId,
            TryResolveHttpOutputPath);

    internal string ReplaceExecuteFlowOutputReferences(string value, IReadOnlyList<ExecuteFlowNodeResult> previousResults) =>
        ReplaceOutputReferences(
            value,
            previousResults,
            result => result.NodeId,
            TryResolveExecuteFlowOutputPath);

    internal string ReplaceCustomFunctionOutputReferences(string value, IReadOnlyList<CustomFunctionNodeResult> previousResults) =>
        ReplaceOutputReferences(
            value,
            previousResults,
            result => result.NodeId,
            TryResolveCustomFunctionOutputPath);

    internal string ReplaceLlmOutputReferences(string value, IReadOnlyList<LlmNodeResult> previousResults) =>
        ReplaceOutputReferences(
            value,
            previousResults,
            result => result.NodeId,
            TryResolveLlmOutputPath);

    internal string ReplaceAgentOutputReferences(string value, IReadOnlyList<AgentNodeResult> previousResults) =>
        ReplaceOutputReferences(
            value,
            previousResults,
            result => result.NodeId,
            TryResolveAgentOutputPath);

    private static string ReplaceOutputReferences<T>(
        string value,
        IReadOnlyList<T> previousResults,
        Func<T, string> nodeIdSelector,
        TryResolveOutputPath<T> tryResolveOutputPath)
    {
        foreach (Match match in OutputReferenceRegex().Matches(value))
        {
            var nodeId = match.Groups["nodeId"].Value.Replace("\\", string.Empty, StringComparison.Ordinal);
            var outputPath = match.Groups["path"].Value;
            var result = previousResults.LastOrDefault(item => string.Equals(nodeIdSelector(item), nodeId, StringComparison.OrdinalIgnoreCase));
            if (result is null)
            {
                continue;
            }

            var resolved = tryResolveOutputPath(result, outputPath, out var outputValue)
                ? outputValue
                : string.Empty;
            value = value == match.Value
                ? resolved
                : value.Replace(match.Value, resolved, StringComparison.Ordinal);
        }

        return value;
    }

    private static bool TryResolveHttpOutputPath(HttpNodeResult result, string path, out string value)
    {
        var output = new
        {
            http = new
            {
                data = result.Data,
                status = result.Status,
                statusText = result.StatusText,
                headers = result.Headers
            }
        };
        return TryResolveSerializedPath(output, path, out value);
    }

    private static bool TryResolveExecuteFlowOutputPath(ExecuteFlowNodeResult result, string path, out string value)
    {
        var output = new
        {
            content = result.Content,
            status = result.Status,
            selectedFlowId = result.SelectedFlowId,
            selectedFlowName = result.SelectedFlowName,
            sourceDocuments = result.SourceDocuments,
            usedTools = result.UsedTools
        };
        return TryResolveSerializedPath(output, path, out value);
    }

    private static bool TryResolveCustomFunctionOutputPath(CustomFunctionNodeResult result, string path, out string value)
    {
        var output = new
        {
            content = result.Content,
            inputVariables = result.InputVariables,
            code = result.Code
        };
        return TryResolveSerializedPath(output, path, out value);
    }

    private static bool TryResolveLlmOutputPath(LlmNodeResult result, string path, out string value)
    {
        var output = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["content"] = result.Content,
            ["returnResponseAs"] = result.ReturnResponseAs,
            ["timeMetadata"] = new
            {
                start = result.StartedAt,
                end = result.CompletedAt,
                delta = (int)Math.Max(0, (result.CompletedAt - result.StartedAt).TotalMilliseconds)
            }
        };
        foreach (var item in result.StructuredOutput)
        {
            output[item.Key] = item.Value;
        }

        return TryResolveSerializedPath(output, path, out value);
    }

    private static bool TryResolveAgentOutputPath(AgentNodeResult result, string path, out string value)
    {
        var output = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["content"] = result.Content,
            ["returnResponseAs"] = result.ReturnResponseAs,
            ["usedTools"] = result.UsedTools,
            ["sourceDocuments"] = result.SourceDocuments,
            ["timeMetadata"] = new
            {
                start = result.StartedAt,
                end = result.CompletedAt,
                delta = (int)Math.Max(0, (result.CompletedAt - result.StartedAt).TotalMilliseconds)
            }
        };
        foreach (var item in result.StructuredOutput)
        {
            output[item.Key] = item.Value;
        }

        return TryResolveSerializedPath(output, path, out value);
    }

    private static bool TryResolveSerializedPath(object output, string path, out string value)
    {
        value = string.Empty;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(output));
        if (!TryReadJsonPath(document.RootElement, ParseReferencePath(path), out var element))
        {
            return false;
        }

        value = FormatJsonElement(element);
        return true;
    }

    private static bool TryReadJsonPath(JsonElement element, IReadOnlyList<string> pathSegments, out JsonElement value)
    {
        value = element;
        foreach (var segment in pathSegments)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(segment, out var index) || index < 0 || index >= value.GetArrayLength())
                {
                    return false;
                }

                value = value[index];
                continue;
            }

            if (value.ValueKind != JsonValueKind.Object || !TryGetJsonProperty(value, segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IReadOnlyList<string> ParseReferencePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var normalized = path
            .Replace("[", ".", StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);
        return normalized
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string FormatJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };

    private delegate bool TryResolveOutputPath<in T>(T result, string path, out string value);

    [GeneratedRegex(@"(?:\{\{\s*)?\$(?<nodeId>[A-Za-z0-9_\\-]+)\.output\.(?<path>[A-Za-z0-9_\.\[\]-]+)(?:\s*\}\})?", RegexOptions.CultureInvariant)]
    private static partial Regex OutputReferenceRegex();
}
