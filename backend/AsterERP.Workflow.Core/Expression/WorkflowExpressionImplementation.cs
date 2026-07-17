using AsterERP.Workflow.Core.Delegate;

namespace AsterERP.Workflow.Core.Expression;

public class WorkflowExpressionImplementation : IWorkflowExpression
{
    private readonly string _expressionText;
    private readonly IExpressionManager _expressionManager;

    public WorkflowExpressionImplementation(string expressionText, IExpressionManager expressionManager)
    {
        _expressionText = expressionText;
        _expressionManager = expressionManager;
    }

    public string ExpressionText => _expressionText;

    public object? GetValue(IDelegateExecution execution)
    {
        return _expressionManager.Evaluate(_expressionText, execution.Variables);
    }

    public void SetValue(IDelegateExecution execution, object? value)
    {
        throw new AsterERP.Workflow.Common.WorkflowEngineException("Setting values via expression is not supported");
    }
}
