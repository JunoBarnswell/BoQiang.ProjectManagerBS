using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class ComplexGatewayActivityBehavior : GatewayActivityBehavior
{
    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }
}
