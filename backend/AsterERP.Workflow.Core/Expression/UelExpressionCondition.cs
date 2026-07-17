using AsterERP.Workflow.Core.Delegate;

namespace AsterERP.Workflow.Core.Expression;

public interface ICondition
{
    bool Evaluate(string activityId, IDelegateExecution execution);
}

public class UelExpressionCondition : ICondition
{
    private readonly IWorkflowExpression _expression;

    public UelExpressionCondition(IWorkflowExpression expression)
    {
        _expression = expression;
    }

    public bool Evaluate(string activityId, IDelegateExecution execution)
    {
        var result = _expression.GetValue(execution);
        if (result is bool boolResult)
            return boolResult;
        return false;
    }
}
