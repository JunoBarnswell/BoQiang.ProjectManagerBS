using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Agenda;

public class ContinueMultiInstanceOperation : AbstractOperation
{
    public ContinueMultiInstanceOperation(IAgenda agenda, ExecutionEntity execution, IProcessEngineConfiguration engineConfig)
        : base(agenda, execution, engineConfig)
    {
    }

    public override async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (Execution == null)
            throw new InvalidOperationException("Execution is required for ContinueMultiInstanceOperation");

        var currentFlowElement = GetCurrentFlowElement(Execution);
        if (currentFlowElement is BpmnModelNs.FlowNode flowNode)
        {
            await ContinueThroughMultiInstanceFlowNode(flowNode, cancellationToken);
        }
        else
        {
            throw new AsterERP.Workflow.Common.WorkflowEngineException(
                $"Programmatic error: no valid multi instance flow node, type: {currentFlowElement?.GetType().Name}. Halting.");
        }
    }

    protected virtual async Task ContinueThroughMultiInstanceFlowNode(BpmnModelNs.FlowNode flowNode, CancellationToken cancellationToken)
    {
        if (!flowNode.Asynchronous)
        {
            await ExecuteSynchronous(flowNode, cancellationToken);
        }
        else
        {
            await ExecuteAsynchronous(flowNode, cancellationToken);
        }
    }

    protected virtual async Task ExecuteSynchronous(BpmnModelNs.FlowNode flowNode, CancellationToken cancellationToken)
    {
        ExecuteExecutionListeners(flowNode, ContinueProcessOperation.EVENTNAME_START);

        RecordActivityStart(Execution!);

        DispatchActivityStartedEvent(flowNode);

        var behavior = flowNode.Behavior as IBpmnActivityBehavior;
        if (behavior is FlowNodeActivityBehavior flowNodeBehavior)
        {
            flowNodeBehavior.Agenda = Agenda;
        }
        if (behavior != null)
        {
            try
            {
                await behavior.ExecuteAsync(Execution!, cancellationToken);
            }
            catch (Common.WorkflowEngineException)
            {
                throw;
            }

            if (Execution!.IsEnded && Execution.Parent != null)
            {
                var miRootExecution = Execution.Parent;
                if (miRootExecution.CurrentFlowElement is BpmnModelNs.FlowNode rootFlowNode && rootFlowNode.Behavior is MultiInstanceActivityBehavior miBehavior)
                {
                    var nrOfCompletedInstances = miBehavior.GetLoopVariable(miRootExecution, MultiInstanceActivityBehavior.NumberOfCompletedInstances) + 1;
                    var nrOfActiveInstances = miBehavior.GetLoopVariable(miRootExecution, MultiInstanceActivityBehavior.NumberOfActiveInstances) - 1;
                    var nrOfInstances = miBehavior.GetLoopVariable(miRootExecution, MultiInstanceActivityBehavior.NumberOfInstances);

                    miBehavior.SetLoopVariable(miRootExecution, MultiInstanceActivityBehavior.NumberOfCompletedInstances, nrOfCompletedInstances);
                    miBehavior.SetLoopVariable(miRootExecution, MultiInstanceActivityBehavior.NumberOfActiveInstances, nrOfActiveInstances);

                    if (miBehavior.CompletionConditionSatisfied(miRootExecution) || nrOfCompletedInstances >= nrOfInstances)
                    {
                        miRootExecution.IsActive = true;
                        await miBehavior.LastExecutionEndedAsync(miRootExecution, cancellationToken);
                    }
                }
            }
        }
    }

    protected virtual async Task ExecuteAsynchronous(BpmnModelNs.FlowNode flowNode, CancellationToken cancellationToken)
    {
        DispatchActivityStartedEvent(flowNode);

        var jobManager = EngineConfig.JobManager;
        if (jobManager != null && Execution != null)
        {
            var job = await jobManager.CreateAsyncJobAsync(
                Execution.Id,
                Execution.ProcessInstanceId!,
                Execution.ProcessDefinitionId!,
                flowNode.Exclusive,
                cancellationToken);

            if (job != null)
            {
                await jobManager.ScheduleAsyncJobAsync(job, cancellationToken);
            }
        }
    }

    private void RecordActivityStart(ExecutionEntity execution)
    {
        EngineConfig.HistoryManager.RecordActivityStart(execution);
    }

    private void DispatchActivityStartedEvent(BpmnModelNs.FlowNode flowNode)
    {
        var eventDispatcher = EngineConfig.EventDispatcher;
        if (eventDispatcher != null)
        {
            var @event = Event.WorkflowEventBuilder.CreateActivityStartedEvent(
                flowNode.Id ?? "",
                flowNode.GetType().Name,
                Execution?.Id,
                Execution?.ProcessInstanceId ?? "");
            eventDispatcher.DispatchEvent(@event);
        }
    }
}
