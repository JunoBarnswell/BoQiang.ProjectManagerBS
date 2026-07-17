using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class CancelEndEventActivityBehavior : EndEventActivityBehavior
{
    public CancelEndEventActivityBehavior() { }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var parentScopeExecution = FindParentSubProcessExecution(execution);
        if (parentScopeExecution == null)
        {
            throw new WorkflowEngineException(
                $"No sub process execution found for cancel end event {execution.CurrentActivityId}");
        }

        var subProcess = parentScopeExecution.CurrentFlowElement as BpmnModelNs.SubProcess;
        if (subProcess == null)
        {
            throw new WorkflowEngineException(
                $"Parent scope execution is not a sub process for cancel end event {execution.CurrentActivityId}");
        }

        var cancelBoundaryEvent = FindCancelBoundaryEvent(subProcess);
        if (cancelBoundaryEvent == null)
        {
            throw new WorkflowEngineException(
                $"Could not find cancel boundary event for cancel end event {execution.CurrentActivityId}");
        }

        var newParentScopeExecution = FindParentScopeExecution(parentScopeExecution);

        execution.SetVariableLocal("_cancelEndEventTriggered", true);
        execution.SetVariableLocal("_cancelTransactionId", parentScopeExecution.GetVariable("_transactionId") as string);

        if (parentScopeExecution.GetVariable("_transactionStarted") is bool transactionStarted && transactionStarted)
        {
            var compensationBehavior = new CompensationEventActivityBehavior();
            await compensationBehavior.ExecuteAsync(parentScopeExecution, cancellationToken);
        }

        DeleteChildExecutions(parentScopeExecution, execution);

        if (newParentScopeExecution != null)
        {
            execution.Parent = newParentScopeExecution;
        }

        execution.CurrentFlowElement = cancelBoundaryEvent;
        execution.CurrentFlowElementId = cancelBoundaryEvent.Id;
        execution.ActivityId = cancelBoundaryEvent.Id;

        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual ExecutionEntity? FindParentSubProcessExecution(ExecutionEntity execution)
    {
        var current = execution.Parent;
        while (current != null)
        {
            if (current.CurrentFlowElement is BpmnModelNs.SubProcess)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    protected virtual ExecutionEntity? FindParentScopeExecution(ExecutionEntity execution)
    {
        var current = execution.Parent;
        while (current != null)
        {
            if (current.IsScope)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    protected virtual BpmnModelNs.BoundaryEvent? FindCancelBoundaryEvent(BpmnModelNs.SubProcess subProcess)
    {
        var boundaryEvents = subProcess.FlowElements
            .OfType<BpmnModelNs.BoundaryEvent>()
            .ToList();

        foreach (var boundaryEvent in boundaryEvents)
        {
            if (boundaryEvent.EventDefinitions != null)
            {
                foreach (var eventDef in boundaryEvent.EventDefinitions)
                {
                    if (eventDef is BpmnModelNs.CancelEventDefinition)
                    {
                        return boundaryEvent;
                    }
                }
            }
        }

        return null;
    }

    protected virtual void DeleteChildExecutions(ExecutionEntity parentExecution, ExecutionEntity notToDeleteExecution)
    {
        if (parentExecution.ChildExecutions == null) return;

        foreach (var childExecution in parentExecution.ChildExecutions.ToList())
        {
            if (childExecution.Id != notToDeleteExecution.Id && !childExecution.IsEnded)
            {
                childExecution.IsEnded = true;
                childExecution.IsActive = false;
                DeleteChildExecutions(childExecution, notToDeleteExecution);
            }
        }
    }
}
