using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class ServiceTaskDelegateExpressionActivityBehavior : FlowNodeActivityBehavior
{
    private readonly IDelegateExpression _delegateExpression;

    public ServiceTaskDelegateExpressionActivityBehavior(IDelegateExpression delegateExpression)
    {
        _delegateExpression = delegateExpression;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var delegateExecution = new DelegateExecution(execution);
        await _delegateExpression.ExecuteAsync(delegateExecution, cancellationToken);
        await LeaveAsync(execution, cancellationToken);
    }
}
