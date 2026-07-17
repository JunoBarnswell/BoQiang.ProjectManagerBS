using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class ServiceTaskDelegateActivityBehavior : FlowNodeActivityBehavior
{
    private readonly IWorkflowDelegate _delegate;

    public ServiceTaskDelegateActivityBehavior(IWorkflowDelegate @delegate)
    {
        _delegate = @delegate;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var delegateExecution = new DelegateExecution(execution);
        await _delegate.ExecuteAsync(delegateExecution);
        await LeaveAsync(execution, cancellationToken);
    }
}
