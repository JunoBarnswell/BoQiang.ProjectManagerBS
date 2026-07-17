using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public interface ITriggerableActivityBehavior : IBpmnActivityBehavior
{
    Task TriggerAsync(ExecutionEntity execution, string? signalName, object? signalData, CancellationToken cancellationToken = default);
}

public interface IInactiveActivityBehavior
{
    Task ExecuteInactiveAsync(ExecutionEntity execution, CancellationToken cancellationToken = default);
}

public abstract class FlowNodeActivityBehavior : IBpmnActivityBehavior
{
    internal IAgenda? Agenda { get; set; }

    public abstract Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default);

    protected virtual Task LeaveAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        Agenda?.PlanTakeOutgoingSequenceFlowsOperation(execution, true);
        return Task.CompletedTask;
    }

    protected virtual Task LeaveAsync(ExecutionEntity execution, IExpressionManager? expressionManager, CancellationToken cancellationToken = default)
    {
        Agenda?.PlanTakeOutgoingSequenceFlowsOperation(execution, true);
        return Task.CompletedTask;
    }

    protected virtual Task LeaveIgnoreConditionsAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        Agenda?.PlanTakeOutgoingSequenceFlowsOperation(execution, false);
        return Task.CompletedTask;
    }
}
