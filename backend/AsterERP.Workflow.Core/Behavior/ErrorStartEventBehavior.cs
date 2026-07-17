using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class ErrorStartEventActivityBehavior : FlowNodeActivityBehavior
{
    public string? ErrorCode { get; set; }

    public ErrorStartEventActivityBehavior() { }

    public ErrorStartEventActivityBehavior(string? errorCode = null)
    {
        ErrorCode = errorCode;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_errorStartEventTriggered", true);
        execution.SetVariableLocal("_errorCode", ErrorCode);

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    public virtual async Task TriggerErrorAsync(ExecutionEntity execution, string? errorCode, CancellationToken cancellationToken = default)
    {
        ErrorCode = errorCode;
        execution.SetVariableLocal("_errorTriggered", true);
        execution.SetVariableLocal("_errorCode", errorCode);
        await ExecuteAsync(execution, cancellationToken);
    }
}
