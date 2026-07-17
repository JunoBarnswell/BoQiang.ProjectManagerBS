using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Agenda;

public class EndExecutionOperation
{
    private readonly IAgenda _agenda;
    private readonly ExecutionEntity _execution;
    private readonly IProcessEngineConfiguration _engineConfig;

    public EndExecutionOperation(IAgenda agenda, ExecutionEntity execution, IProcessEngineConfiguration engineConfig)
    {
        _agenda = agenda;
        _execution = execution;
        _engineConfig = engineConfig;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_execution.IsProcessInstanceType || _execution.ParentId == null)
        {
            await HandleProcessInstanceExecutionAsync(_execution, cancellationToken);
        }
        else
        {
            HandleRegularExecution();
        }

        await Task.CompletedTask;
    }

    private async Task HandleProcessInstanceExecutionAsync(ExecutionEntity processInstanceExecution, CancellationToken cancellationToken)
    {
        var activeChildExecutions = CountActiveChildExecutions(processInstanceExecution);

        if (activeChildExecutions == 0)
        {
            DispatchProcessCompletedEvent();

            _engineConfig.HistoryManager.RecordProcessInstanceEnd(processInstanceExecution, null);

            processInstanceExecution.IsActive = false;
            processInstanceExecution.IsEnded = true;
        }

        if (processInstanceExecution.SuperExecutionId != null && processInstanceExecution.Parent != null)
        {
            var superExecution = processInstanceExecution.Parent;
            if (superExecution.CurrentFlowElement is BpmnModelNs.FlowNode flowNode &&
                flowNode.Behavior is CallActivityBehavior callActivityBehavior)
            {
                callActivityBehavior.Agenda = _agenda;
                await callActivityBehavior.CompletingAsync(superExecution, processInstanceExecution.Id, cancellationToken);
                await callActivityBehavior.CompletedAsync(superExecution, cancellationToken);
            }
            else
            {
                _agenda.PlanTakeOutgoingSequenceFlowsOperation(superExecution, true);
            }
        }
    }

    private void HandleRegularExecution()
    {
        var errorThrown = _execution.GetVariable("_errorThrown") as bool? == true;
        var errorCode = _execution.GetVariable("_errorCode") as string;

        _execution.IsActive = false;
        _execution.IsEnded = true;

        if (_execution.IsScope)
        {
            DeleteChildExecutions(_execution);
        }

        CleanupVariables(_execution);

        if (_execution.Parent != null)
        {
            var parent = _execution.Parent;
            var allChildrenEnded = parent.ChildExecutions.All(child => child.IsEnded);

            if (allChildrenEnded)
            {
                if (parent.CurrentFlowElement is BpmnModelNs.Transaction)
                {
                    if (errorThrown)
                    {
                        throw new WorkflowEngineException($"Transaction sub-process ended with error{(errorCode != null ? $", code: '{errorCode}'" : "")}");
                    }
                    _agenda.PlanDestroyScopeOperation(parent);
                }
                else if (parent.CurrentFlowElement is BpmnModelNs.SubProcess)
                {
                    _agenda.PlanDestroyScopeOperation(parent);
                }
                else if (parent.IsProcessInstanceType || parent.ParentId == null)
                {
                    _agenda.PlanEndExecutionOperation(parent);
                }
                else if (parent.IsScope)
                {
                    _agenda.PlanDestroyScopeOperation(parent);
                }
                else
                {
                    _agenda.PlanEndExecutionOperation(parent);
                }
            }
        }
    }

    private int CountActiveChildExecutions(ExecutionEntity execution)
    {
        int count = 0;
        foreach (var child in execution.ChildExecutions)
        {
            if (child.IsActive && !child.IsEnded)
            {
                count++;
            }
            count += CountActiveChildExecutions(child);
        }
        return count;
    }

    private void DeleteChildExecutions(ExecutionEntity execution)
    {
        foreach (var child in execution.ChildExecutions.ToList())
        {
            DeleteChildExecutions(child);
            child.IsActive = false;
            child.IsEnded = true;
            CleanupVariables(child);
        }
        execution.ChildExecutions.Clear();
    }

    private void CleanupVariables(ExecutionEntity execution)
    {
        execution.Variables.Clear();
    }

    private void DispatchProcessCompletedEvent()
    {
        var eventDispatcher = _engineConfig.EventDispatcher;
        if (eventDispatcher != null)
        {
            var @event = WorkflowEventBuilder.CreateProcessCompletedEvent(
                _execution.ProcessInstanceId ?? _execution.Id,
                _execution.ProcessDefinitionId ?? "");
            eventDispatcher.DispatchEvent(@event);
        }
    }
}
