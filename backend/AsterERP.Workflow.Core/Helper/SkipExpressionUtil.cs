using System.Collections.Generic;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Helper;

public static class SkipExpressionUtil
{
    public const string SkipExpressionEnabledVariable = "_ACTIVITI_SKIP_EXPRESSION_ENABLED";

    public static bool IsSkipExpressionEnabled(ExecutionEntity execution)
    {
        if (execution.GetVariable(SkipExpressionEnabledVariable) is bool enabled)
            return enabled;

        return false;
    }

    public static bool ShouldSkipFlowElement(
        string? skipExpression,
        ExecutionEntity execution,
        IExpressionManager expressionManager)
    {
        if (string.IsNullOrWhiteSpace(skipExpression) || !IsSkipExpressionEnabled(execution))
            return false;

        var variables = new Dictionary<string, object?>(execution.Variables);
        var result = expressionManager.Evaluate(skipExpression, variables);
        return result is bool boolResult && boolResult;
    }
}
