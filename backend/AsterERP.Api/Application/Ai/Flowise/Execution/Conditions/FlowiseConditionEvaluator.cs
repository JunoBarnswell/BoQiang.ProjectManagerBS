using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseConditionEvaluator(
    FlowiseRuntimeNodeClassifier nodeClassifier,
    FlowiseRuntimeNodeDataReader nodeDataReader,
    FlowiseVariableResolver variableResolver)
{
    internal IReadOnlyDictionary<string, BranchDecision> ResolveBranchDecisions(
        FlowiseRuntimeFlowData flowData,
        FlowiseExecutionContext context)
    {
        var decisions = new Dictionary<string, BranchDecision>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in flowData.Nodes.Where(nodeClassifier.IsConditionNode))
        {
            var edges = flowData.Edges
                .Where(edge => string.Equals(edge.Source, node.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(edge => ResolveEdgeOutputIndex(edge, node.Id))
                .ToList();
            if (edges.Count == 0)
            {
                continue;
            }

            var matchedIndex = ResolveConditionIndex(node, context, edges.Count);
            var selectedEdge = edges[Math.Clamp(matchedIndex, 0, edges.Count - 1)];
            decisions[node.Id] = new BranchDecision(matchedIndex, selectedEdge.SourceHandle, selectedEdge.Target);
        }

        return decisions;
    }

    internal int ResolveEdgeOutputIndex(FlowiseRuntimeEdge edge, string nodeId)
    {
        var handle = edge.SourceHandle ?? string.Empty;
        var marker = $"{nodeId}-output-";
        var markerIndex = handle.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0 && int.TryParse(handle[(markerIndex + marker.Length)..], out var indexedHandle))
        {
            return indexedHandle;
        }

        if (handle.Contains("false", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (handle.Contains("true", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var lastDash = handle.LastIndexOf('-');
        if (lastDash >= 0 && int.TryParse(handle[(lastDash + 1)..], out var trailingIndex))
        {
            return trailingIndex;
        }

        return 0;
    }

    private int ResolveConditionIndex(FlowiseRuntimeNode node, FlowiseExecutionContext context, int outputCount)
    {
        var conditions = ReadConditionDefinitions(node.Data);
        if (conditions.Count > 0)
        {
            for (var index = 0; index < conditions.Count; index++)
            {
                if (EvaluateConditionRule(conditions[index], context))
                {
                    return Math.Min(index, outputCount - 1);
                }
            }

            return Math.Min(conditions.Count, outputCount - 1);
        }

        var expression = nodeDataReader.ReadNodeInputString(node.Data, "expression") ??
            nodeDataReader.ReadNodeInputString(node.Data, "condition") ??
            nodeDataReader.ReadNodeInputString(node.Data, "value");
        return EvaluateConditionExpression(expression, context) ? 0 : Math.Min(1, outputCount - 1);
    }

    private IReadOnlyList<ConditionRule> ReadConditionDefinitions(IReadOnlyDictionary<string, JsonElement> data)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, "conditions", out var conditions) || conditions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<ConditionRule>();
        foreach (var item in conditions.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                result.Add(new ConditionRule("string", item.GetString() ?? string.Empty, "contains", "$question", item.GetString() ?? string.Empty));
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                var expression = nodeDataReader.ReadString(item, "expression") ??
                    nodeDataReader.ReadString(item, "condition") ??
                    nodeDataReader.ReadString(item, "label");
                if (!string.IsNullOrWhiteSpace(expression))
                {
                    result.Add(new ConditionRule("string", expression, "expression", string.Empty, string.Empty));
                    continue;
                }

                result.Add(new ConditionRule(
                    nodeDataReader.ReadString(item, "type") ?? "string",
                    string.Empty,
                    nodeDataReader.ReadString(item, "operation") ?? "equal",
                    nodeDataReader.ReadJsonPropertyAsString(item, "value1"),
                    nodeDataReader.ReadJsonPropertyAsString(item, "value2")));
            }
        }

        return result;
    }

    private bool EvaluateConditionRule(ConditionRule rule, FlowiseExecutionContext context)
    {
        if (rule.Operation.Equals("expression", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateConditionExpression(rule.Expression, context);
        }

        var left = ResolveConditionValue(rule.Value1, context);
        var right = ResolveConditionValue(rule.Value2, context);
        if (rule.Type.Equals("number", StringComparison.OrdinalIgnoreCase))
        {
            var leftNumber = ParseFlowiseNumber(left);
            var rightNumber = ParseFlowiseNumber(right);
            return rule.Operation switch
            {
                "larger" => leftNumber > rightNumber,
                "largerEqual" => leftNumber >= rightNumber,
                "smaller" => leftNumber < rightNumber,
                "smallerEqual" => leftNumber <= rightNumber,
                "notEqual" => leftNumber != rightNumber,
                "isEmpty" => string.IsNullOrEmpty(left),
                "notEmpty" => !string.IsNullOrEmpty(left),
                _ => leftNumber == rightNumber
            };
        }

        if (rule.Type.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            var leftBool = ParseFlowiseBoolean(left);
            var rightBool = ParseFlowiseBoolean(right);
            return rule.Operation.Equals("notEqual", StringComparison.OrdinalIgnoreCase)
                ? leftBool != rightBool
                : leftBool == rightBool;
        }

        left = StripMarkdown(left);
        right = StripMarkdown(right);
        return rule.Operation switch
        {
            "contains" => left.Contains(right, StringComparison.Ordinal),
            "notContains" => !left.Contains(right, StringComparison.Ordinal),
            "endsWith" => left.EndsWith(right, StringComparison.Ordinal),
            "notEqual" => !left.Equals(right, StringComparison.Ordinal),
            "regex" => EvaluateRegexCondition(left, right),
            "startsWith" => left.StartsWith(right, StringComparison.Ordinal),
            "isEmpty" => string.IsNullOrEmpty(left),
            "notEmpty" => !string.IsNullOrEmpty(left),
            _ => left.Equals(right, StringComparison.Ordinal)
        };
    }

    private string ResolveConditionValue(string value, FlowiseExecutionContext context) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : variableResolver.ReplaceRuntimeVariables(value, context, null, []);

    private static decimal ParseFlowiseNumber(string value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    private static bool ParseFlowiseBoolean(string value) =>
        bool.TryParse(value, out var parsed) ? parsed : string.Equals(value, "1", StringComparison.Ordinal);

    private static bool EvaluateRegexCondition(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, UnescapeFlowiseRegexPattern(pattern));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string UnescapeFlowiseRegexPattern(string pattern) =>
        pattern
            .Replace("\\\\", "\0", StringComparison.Ordinal)
            .Replace("\\[", "[", StringComparison.Ordinal)
            .Replace("\\]", "]", StringComparison.Ordinal)
            .Replace("\\*", "*", StringComparison.Ordinal)
            .Replace("\0", "\\", StringComparison.Ordinal);

    private static string StripMarkdown(string value) =>
        Regex.Replace(value, @"[*_`#>\[\]\(\)!]", string.Empty, RegexOptions.CultureInvariant);

    private static bool EvaluateConditionExpression(string? expression, FlowiseExecutionContext context)
    {
        var normalized = TrimConditionExpression(expression);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var question = context.Question?.Trim() ?? string.Empty;
        if (TryReadFunctionArgument(normalized, "contains", out var contains) ||
            TryReadFunctionArgument(normalized, "includes", out contains))
        {
            return question.Contains(contains, StringComparison.OrdinalIgnoreCase);
        }

        if (TryReadBinaryCondition(normalized, "contains", out var _, out var right))
        {
            return question.Contains(right, StringComparison.OrdinalIgnoreCase);
        }

        if (TryReadBinaryCondition(normalized, "==", out var left, out right))
        {
            return ResolveConditionOperand(left, context).Equals(ResolveConditionOperand(right, context), StringComparison.OrdinalIgnoreCase);
        }

        if (TryReadBinaryCondition(normalized, "!=", out left, out right))
        {
            return !ResolveConditionOperand(left, context).Equals(ResolveConditionOperand(right, context), StringComparison.OrdinalIgnoreCase);
        }

        return question.Contains(Unquote(normalized), StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimConditionExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        var trimmed = expression.Trim();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[') ? string.Empty : trimmed;
    }

    private static bool TryReadFunctionArgument(string expression, string functionName, out string argument)
    {
        argument = string.Empty;
        var prefix = functionName + "(";
        if (!expression.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !expression.EndsWith(')'))
        {
            return false;
        }

        argument = Unquote(expression[prefix.Length..^1].Trim());
        return !string.IsNullOrWhiteSpace(argument);
    }

    private static bool TryReadBinaryCondition(string expression, string op, out string left, out string right)
    {
        left = string.Empty;
        right = string.Empty;
        var index = expression.IndexOf(op, StringComparison.OrdinalIgnoreCase);
        if (index <= 0)
        {
            return false;
        }

        left = expression[..index].Trim();
        right = expression[(index + op.Length)..].Trim();
        return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right);
    }

    private static string ResolveConditionOperand(string operand, FlowiseExecutionContext context)
    {
        var normalized = Unquote(operand);
        if (normalized.Equals("question", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("$question", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("input", StringComparison.OrdinalIgnoreCase))
        {
            return context.Question?.Trim() ?? string.Empty;
        }

        return normalized;
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '\'' && trimmed[^1] == '\''))
            ? trimmed[1..^1]
            : trimmed;
    }
}
