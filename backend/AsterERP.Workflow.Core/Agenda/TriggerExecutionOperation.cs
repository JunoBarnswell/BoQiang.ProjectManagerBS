using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Agenda;

public class TriggerExecutionOperation : AbstractOperation
{
    public TriggerExecutionOperation(IAgenda agenda, ExecutionEntity execution, IProcessEngineConfiguration engineConfig)
        : base(agenda, execution, engineConfig)
    {
    }

    public override async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (Execution == null)
            throw new InvalidOperationException("Execution is required for TriggerExecutionOperation");

        var currentFlowElement = GetCurrentFlowElement(Execution);
        if (currentFlowElement is BpmnModelNs.FlowNode flowNode)
        {
            var activityBehavior = flowNode.Behavior as Behavior.IBpmnActivityBehavior;
            if (activityBehavior is Behavior.FlowNodeActivityBehavior flowNodeBehavior)
            {
                flowNodeBehavior.Agenda = Agenda;
            }
            if (activityBehavior is Behavior.ITriggerableActivityBehavior triggerableBehavior)
            {
                if (currentFlowElement is BpmnModelNs.BoundaryEvent)
                {
                    RecordActivityStart(Execution);
                }

                await triggerableBehavior.TriggerAsync(Execution, null, null, cancellationToken);

                if (currentFlowElement is BpmnModelNs.BoundaryEvent)
                {
                    RecordActivityEnd(Execution);
                }
            }
            else
            {
                throw new AsterERP.Workflow.Common.WorkflowEngineException(
                    $"Invalid behavior: {activityBehavior?.GetType().Name} should implement ITriggerableActivityBehavior");
            }
        }
        else
        {
            throw new AsterERP.Workflow.Common.WorkflowEngineException(
                $"Programmatic error: no current flow element found or invalid type: {currentFlowElement?.GetType().Name}. Halting.");
        }
    }

    private void RecordActivityStart(ExecutionEntity execution)
    {
        EngineConfig.HistoryManager.RecordActivityStart(execution);
    }

    private void RecordActivityEnd(ExecutionEntity execution)
    {
        EngineConfig.HistoryManager.RecordActivityEnd(execution, null);
    }
}
