using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowConditionEvaluator
{
    private static readonly Regex ComparisonRegex = new(
        @"^\s*(?<left>[A-Za-z_][A-Za-z0-9_.]*)\s*(?<op>==|!=|>=|<=|>|<)\s*(?<right>'[^']*'|""[^""]*""|-?\d+(?:\.\d+)?|true|false|[A-Za-z_][A-Za-z0-9_.]*)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool Evaluate(string? expression, IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return true;
        }

        EnsureSafeExpression(expression);
        var orParts = Regex.Split(expression, @"\s+or\s+", RegexOptions.IgnoreCase)
            .Where(item => !string.IsNullOrWhiteSpace(item));
        return orParts.Any(part => Regex.Split(part, @"\s+and\s+", RegexOptions.IgnoreCase)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .All(item => EvaluateComparison(item, variables)));
    }

    public string? Validate(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        try
        {
            EnsureSafeExpression(expression);
            var parts = Regex.Split(expression, @"\s+(?:and|or)\s+", RegexOptions.IgnoreCase)
                .Where(item => !string.IsNullOrWhiteSpace(item));
            var invalid = parts.FirstOrDefault(item => !ComparisonRegex.IsMatch(item));
            return invalid is null ? null : $"条件片段不受支持：{invalid.Trim()}";
        }
        catch (ValidationException ex)
        {
            return ex.Message;
        }
    }

    private static void EnsureSafeExpression(string expression)
    {
        if (expression.Length > 300 ||
            expression.Contains(';') ||
            expression.Contains('(') ||
            expression.Contains(')') ||
            expression.Contains("sql", StringComparison.OrdinalIgnoreCase) ||
            expression.Contains("script", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("条件表达式只支持字段比较与 and/or，不允许脚本、SQL 或函数调用", ErrorCodes.AiWorkflowConditionInvalid);
        }
    }

    private static bool EvaluateComparison(string comparison, IReadOnlyDictionary<string, object?> variables)
    {
        var match = ComparisonRegex.Match(comparison);
        if (!match.Success)
        {
            throw new ValidationException($"条件表达式不受支持：{comparison}", ErrorCodes.AiWorkflowConditionInvalid);
        }

        var left = ResolveValue(match.Groups["left"].Value, variables);
        var right = ResolveLiteral(match.Groups["right"].Value, variables);
        return Compare(left, right, match.Groups["op"].Value);
    }

    private static object? ResolveValue(string name, IReadOnlyDictionary<string, object?> variables)
    {
        return variables.TryGetValue(name, out var value) ? NormalizeJsonValue(value) : null;
    }

    private static object? ResolveLiteral(string token, IReadOnlyDictionary<string, object?> variables)
    {
        if ((token.StartsWith('\'') && token.EndsWith('\'')) ||
            (token.StartsWith('"') && token.EndsWith('"')))
        {
            return token[1..^1];
        }

        if (decimal.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue;
        }

        if (bool.TryParse(token, out var boolValue))
        {
            return boolValue;
        }

        return ResolveValue(token, variables);
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => element.ToString()
            };
        }

        return value;
    }

    private static bool Compare(object? left, object? right, string op)
    {
        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber))
        {
            return op switch
            {
                "==" => leftNumber == rightNumber,
                "!=" => leftNumber != rightNumber,
                ">=" => leftNumber >= rightNumber,
                "<=" => leftNumber <= rightNumber,
                ">" => leftNumber > rightNumber,
                "<" => leftNumber < rightNumber,
                _ => false
            };
        }

        var leftText = left?.ToString() ?? string.Empty;
        var rightText = right?.ToString() ?? string.Empty;
        var compare = string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "==" => compare == 0,
            "!=" => compare != 0,
            ">=" => compare >= 0,
            "<=" => compare <= 0,
            ">" => compare > 0,
            "<" => compare < 0,
            _ => false
        };
    }

    private static bool TryDecimal(object? value, out decimal result)
    {
        if (value is decimal decimalValue)
        {
            result = decimalValue;
            return true;
        }

        return decimal.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}
