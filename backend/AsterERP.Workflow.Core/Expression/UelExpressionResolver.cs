namespace AsterERP.Workflow.Core.Expression;

public static class UelExpressionResolver
{
    private const string ValueExpressionPrefix = "${";
    private const string MethodExpressionPrefix = "#{";
    private const string ExpressionSuffix = "}";

    public static bool IsExpression(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return IsValueExpression(text) || IsMethodExpression(text);
    }

    public static string? ExtractExpression(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.StartsWith(ValueExpressionPrefix) && text.EndsWith(ExpressionSuffix))
        {
            return text.Substring(2, text.Length - 3).Trim();
        }

        if (text.StartsWith(MethodExpressionPrefix) && text.EndsWith(ExpressionSuffix))
        {
            return text.Substring(2, text.Length - 3).Trim();
        }

        return null;
    }

    public static bool IsValueExpression(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.StartsWith(ValueExpressionPrefix) && text.EndsWith(ExpressionSuffix);
    }

    public static bool IsMethodExpression(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.StartsWith(MethodExpressionPrefix) && text.EndsWith(ExpressionSuffix);
    }
}
