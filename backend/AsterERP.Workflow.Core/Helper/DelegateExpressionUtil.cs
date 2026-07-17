using System.Collections.Generic;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Helper;

public static class DelegateExpressionUtil
{
    public static object ResolveDelegateExpression(
        string delegateExpression,
        IExpressionManager? expressionManager = null,
        Dictionary<string, object?>? variables = null,
        IServiceProvider? serviceProvider = null)
    {
        if (string.IsNullOrWhiteSpace(delegateExpression))
            throw new WorkflowEngineArgumentException("delegateExpression is null or empty");

        var expressionText = StripExpressionDelimiters(delegateExpression);
        if (expressionManager != null)
        {
            var resolved = expressionManager.Evaluate(expressionText, variables ?? new Dictionary<string, object?>());
            if (resolved != null)
                return resolved;
        }

        return ClassDelegateUtil.Instantiate(expressionText, serviceProvider);
    }

    public static T ResolveDelegateExpression<T>(
        string delegateExpression,
        IExpressionManager? expressionManager = null,
        Dictionary<string, object?>? variables = null,
        IServiceProvider? serviceProvider = null) where T : class
    {
        var resolved = ResolveDelegateExpression(delegateExpression, expressionManager, variables, serviceProvider);
        if (resolved is T typed)
            return typed;

        throw new WorkflowEngineArgumentException(
            $"Delegate expression '{delegateExpression}' did not resolve to {typeof(T).FullName}");
    }

    public static string StripExpressionDelimiters(string delegateExpression)
    {
        var expression = delegateExpression.Trim();
        if ((expression.StartsWith("${") || expression.StartsWith("#{")) && expression.EndsWith("}"))
            return expression[2..^1].Trim();

        return expression;
    }
}
