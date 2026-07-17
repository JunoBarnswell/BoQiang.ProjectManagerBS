using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class ReceiveTaskActivityBehavior : FlowNodeActivityBehavior, ITriggerableActivityBehavior
{
    public bool WaitForCallback { get; set; } = true;
    public BpmnModelNs.ReceiveTask? ReceiveTask { get; set; }

    public ReceiveTaskActivityBehavior() { }

    public ReceiveTaskActivityBehavior(BpmnModelNs.ReceiveTask receiveTask)
    {
        ReceiveTask = receiveTask;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.IsActive = false;
        execution.SetVariable("_receiveTaskWaiting", true);
        execution.SetVariable("_receiveTaskId", execution.CurrentActivityId);
        execution.SetVariableLocal("_receiveTaskSubscriptionActive", true);
    }

    public virtual async Task SignalAsync(ExecutionEntity execution, string? signalName = null, object? signalData = null, CancellationToken cancellationToken = default)
    {
        execution.SetVariable("_receiveTaskWaiting", false);
        await LeaveAsync(execution, cancellationToken);
    }

    public virtual async Task TriggerAsync(ExecutionEntity execution, string? signalName = null, object? signalData = null, CancellationToken cancellationToken = default)
    {
        await SignalAsync(execution, signalName, signalData, cancellationToken);
    }

    public virtual bool IsWaiting(ExecutionEntity execution)
    {
        return execution.GetVariable("_receiveTaskWaiting") is bool b && b;
    }

    public virtual string? GetWaitActivityId(ExecutionEntity execution)
    {
        return execution.GetVariable("_receiveTaskId") as string;
    }
}
