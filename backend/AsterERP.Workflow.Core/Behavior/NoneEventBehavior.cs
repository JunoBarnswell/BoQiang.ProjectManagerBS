using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class IntermediateThrowNoneEventActivityBehavior : FlowNodeActivityBehavior
{
    public IntermediateThrowNoneEventActivityBehavior() { }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }
}
