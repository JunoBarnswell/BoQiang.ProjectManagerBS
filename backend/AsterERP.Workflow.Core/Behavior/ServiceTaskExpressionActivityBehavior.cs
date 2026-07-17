using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Behavior;

public class ServiceTaskExpressionActivityBehavior : FlowNodeActivityBehavior
{
    private readonly IWorkflowExpression _expression;
    private readonly string? _resultVariableName;

    public ServiceTaskExpressionActivityBehavior(IWorkflowExpression expression, string? resultVariableName = null)
    {
        _expression = expression;
        _resultVariableName = resultVariableName;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var delegateExecution = new DelegateExecution(execution);
        var result = _expression.GetValue(delegateExecution);

        if (!string.IsNullOrEmpty(_resultVariableName) && result != null)
        {
            execution.SetVariable(_resultVariableName, result);
        }

        await LeaveAsync(execution, cancellationToken);
    }
}
