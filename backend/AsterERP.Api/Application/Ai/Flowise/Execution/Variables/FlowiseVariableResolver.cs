using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed partial class FlowiseVariableResolver
{
    internal string ReplaceRuntimeVariables(
        string value,
        FlowiseExecutionContext context,
        FlowiseIterationContext? iterationContext,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults)
    {
        var result = value
            .Replace("$question", context.Question ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{question}}", context.Question ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        result = ReplaceChatContextReferences(result, context);
        result = ReplaceFormReferences(result, context.Form);
        result = ReplaceWebhookReferences(result, context.Webhook);
        result = ReplaceFlowReferences(result, context);
        if (iterationContext is not null)
        {
            result = result
                .Replace("$iteration.index", iterationContext.Index.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("$iteration.value", iterationContext.Value, StringComparison.OrdinalIgnoreCase)
                .Replace("$iteration", iterationContext.Value, StringComparison.OrdinalIgnoreCase)
                .Replace("{{iteration.index}}", iterationContext.Index.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
                .Replace("{{iteration.value}}", iterationContext.Value, StringComparison.OrdinalIgnoreCase)
                .Replace("{{iteration}}", iterationContext.Value, StringComparison.OrdinalIgnoreCase);
        }

        return ReplaceOutputReferences(result, previousResults);
    }

    private static string ReplaceChatContextReferences(string value, FlowiseExecutionContext context)
    {
        foreach (Match match in ChatContextReferenceRegex().Matches(value))
        {
            var name = match.Groups["name"].Value;
            var replacement = name.ToLowerInvariant() switch
            {
                "question" => context.Question ?? string.Empty,
                "chat_history" => FormatChatHistory(context.ChatHistory),
                "current_date_time" => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                "runtime_messages_length" => context.ChatHistory.Count.ToString(CultureInfo.InvariantCulture),
                "file_attachment" => string.Empty,
                "loop_count" => context.CurrentLoopCount.ToString(CultureInfo.InvariantCulture),
                _ => null
            };
            if (replacement is null)
            {
                continue;
            }

            value = value == match.Value
                ? replacement
                : value.Replace(match.Value, replacement, StringComparison.Ordinal);
        }

        return value;
    }

    private static string ReplaceFlowReferences(string value, FlowiseExecutionContext context)
    {
        var flow = context.BuildFlowVariables();
        foreach (Match match in FlowReferenceRegex().Matches(value))
        {
            var path = match.Groups["path"].Value;
            if (!TryResolveObjectPath(flow, ParseReferencePath(path), out var resolved))
            {
                continue;
            }

            var formatted = FormatVariableValue(resolved);
            value = value == match.Value
                ? formatted
                : value.Replace(match.Value, formatted, StringComparison.Ordinal);
        }

        return value;
    }

    private static string ReplaceWebhookReferences(string value, IReadOnlyDictionary<string, object?> webhook) =>
        ReplaceObjectReferences(value, webhook, WebhookReferenceRegex());

    private static string ReplaceFormReferences(string value, IReadOnlyDictionary<string, object?> form) =>
        ReplaceObjectReferences(value, form, FormReferenceRegex());

    private static string ReplaceObjectReferences(string value, IReadOnlyDictionary<string, object?> source, Regex regex)
    {
        foreach (Match match in regex.Matches(value))
        {
            var path = match.Groups["path"].Value;
            if (!TryResolveObjectPath(source, ParseReferencePath(path), out var resolved))
            {
                continue;
            }

            var formatted = FormatVariableValue(resolved);
            value = value == match.Value
                ? formatted
                : value.Replace(match.Value, formatted, StringComparison.Ordinal);
        }

        return value;
    }

    private static string ReplaceOutputReferences(string value, IReadOnlyList<RuntimeDataModelNodeResult> previousResults)
    {
        foreach (Match match in OutputReferenceRegex().Matches(value))
        {
            var nodeId = match.Groups["nodeId"].Value.Replace("\\", string.Empty, StringComparison.Ordinal);
            var outputPath = match.Groups["path"].Value;
            var runtimeResult = previousResults.LastOrDefault(result => string.Equals(result.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
            if (runtimeResult is null)
            {
                continue;
            }

            var resolved = TryResolveRuntimeOutputPath(runtimeResult, outputPath, out var outputValue)
                ? outputValue
                : string.Empty;
            value = value == match.Value
                ? resolved
                : value.Replace(match.Value, resolved, StringComparison.Ordinal);
        }

        return value;
    }

    private static bool TryResolveRuntimeOutputPath(RuntimeDataModelNodeResult result, string path, out string value)
    {
        value = string.Empty;
        var outputJson = JsonSerializer.Serialize(new
        {
            total = result.Response.Total,
            pageIndex = result.Response.PageIndex,
            pageSize = result.Response.PageSize,
            fields = result.Response.Fields,
            rows = result.Response.Rows,
            rowCount = result.Response.Rows.Count,
            content = BuildRuntimeModelSummary(result)
        });
        using var document = JsonDocument.Parse(outputJson);
        if (!TryReadJsonPath(document.RootElement, ParseReferencePath(path), out var element))
        {
            return false;
        }

        value = FormatJsonElement(element);
        return true;
    }

    private static bool TryResolveObjectPath(object? current, IReadOnlyList<string> pathSegments, out object? value)
    {
        value = current;
        foreach (var segment in pathSegments)
        {
            switch (value)
            {
                case IReadOnlyDictionary<string, object?> dictionary:
                    var key = dictionary.Keys.FirstOrDefault(item => item.Equals(segment, StringComparison.OrdinalIgnoreCase));
                    if (key is null)
                    {
                        return false;
                    }

                    value = dictionary[key];
                    break;
                case JsonElement element:
                    if (!TryReadJsonPath(element, [segment], out var nextElement))
                    {
                        return false;
                    }

                    value = nextElement;
                    break;
                default:
                    return false;
            }
        }

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

    private static string FormatVariableValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is JsonElement element)
        {
            return FormatJsonElement(element);
        }

        return value is string text
            ? text
            : JsonSerializer.Serialize(value);
    }

    private static string FormatJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };

    private static string FormatChatHistory(IReadOnlyList<FlowiseChatHistoryMessageDto> chatHistory)
    {
        if (chatHistory.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            chatHistory
                .Where(item => !string.IsNullOrWhiteSpace(item.Content))
                .TakeLast(20)
                .Select(item =>
                {
                    var role = string.IsNullOrWhiteSpace(item.Role) ? "user" : item.Role.Trim();
                    return $"{role}: {item.Content.Trim()}";
                }));
    }

    private static string BuildRuntimeModelSummary(RuntimeDataModelNodeResult result)
    {
        var visibleFields = result.Response.Fields
            .Where(field => field.Visible)
            .OrderBy(field => field.Order)
            .Take(8)
            .ToList();
        var builder = new StringBuilder();
        builder.Append("系统配置模型 ");
        builder.Append(result.ModelCode);
        builder.Append(" 查询完成：共 ");
        builder.Append(result.Response.Total);
        builder.Append(" 条，当前返回 ");
        builder.Append(result.Response.Rows.Count);
        builder.Append(" 条。");

        if (result.Response.Rows.Count == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine();
        var rowIndex = 1;
        foreach (var row in result.Response.Rows.Take(5))
        {
            var cells = visibleFields
                .Select(field => $"{field.FieldName}={FormatCellValue(row.TryGetValue(field.FieldCode, out var value) ? value : null)}")
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
            builder.Append(rowIndex);
            builder.Append(". ");
            builder.Append(string.Join("；", cells));
            builder.AppendLine();
            rowIndex += 1;
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatCellValue(object? value) =>
        value switch
        {
            null => string.Empty,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element when element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value) ?? string.Empty
        };

    [GeneratedRegex(@"(?:\{\{\s*)?\$(?<nodeId>[A-Za-z0-9_\\-]+)\.output\.(?<path>[A-Za-z0-9_\.\[\]-]+)(?:\s*\}\})?", RegexOptions.CultureInvariant)]
    private static partial Regex OutputReferenceRegex();

    [GeneratedRegex(@"(?:\{\{\s*)?\$form\.(?<path>[A-Za-z0-9_\.\[\]-]+)(?:\s*\}\})?", RegexOptions.CultureInvariant)]
    private static partial Regex FormReferenceRegex();

    [GeneratedRegex(@"(?:\{\{\s*)?\$flow\.(?<path>[A-Za-z0-9_\.\[\]-]+)(?:\s*\}\})?", RegexOptions.CultureInvariant)]
    private static partial Regex FlowReferenceRegex();

    [GeneratedRegex(@"(?:\{\{\s*)?\$webhook\.(?<path>[A-Za-z0-9_\.\[\]-]+)(?:\s*\}\})?", RegexOptions.CultureInvariant)]
    private static partial Regex WebhookReferenceRegex();

    [GeneratedRegex(@"\{\{\s*(?<name>question|chat_history|current_date_time|runtime_messages_length|file_attachment|loop_count)\s*\}\}", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ChatContextReferenceRegex();
}
