using AsterERP.Workflow.Core.Delegate;

namespace AsterERP.Workflow.Core.Expression;

public interface IWorkflowExpression
{
    string ExpressionText { get; }
    object? GetValue(IDelegateExecution execution);
    void SetValue(IDelegateExecution execution, object? value);
}
