using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Helper;
using AsterERP.Workflow.Core.Util;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Agenda;

public class TakeOutgoingSequenceFlowsOperation
{
    private readonly IAgenda _agenda;
    private readonly ExecutionEntity _execution;
    private readonly bool _evaluateConditions;
    private readonly IProcessEngineConfiguration _engineConfig;

    public TakeOutgoingSequenceFlowsOperation(IAgenda agenda, ExecutionEntity execution, bool evaluateConditions, IProcessEngineConfiguration engineConfig)
    {
        _agenda = agenda;
        _execution = execution;
        _evaluateConditions = evaluateConditions;
        _engineConfig = engineConfig;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var currentFlowElement = _execution.CurrentFlowElement;

        await CleanupExecutions(currentFlowElement);

        if (currentFlowElement is BpmnModelNs.FlowNode flowNode)
        {
            await LeaveFlowNode(flowNode, cancellationToken);
        }
    }

    private async Task CleanupExecutions(BpmnModelNs.FlowElement? currentFlowElement)
    {
        if (currentFlowElement is BpmnModelNs.Activity activity && activity.BoundaryEvents != null)
        {
            var executionsToRemove = _execution.ChildExecutions
                .Where(child => !child.IsEnded && child.CurrentFlowElement is BpmnModelNs.BoundaryEvent)
                .ToList();

            foreach (var child in executionsToRemove)
            {
                child.IsActive = false;
                child.IsEnded = true;
                _execution.ChildExecutions.Remove(child);
            }
        }

        await Task.CompletedTask;
    }

    private async Task LeaveFlowNode(BpmnModelNs.FlowNode flowNode, CancellationToken cancellationToken)
    {
        await ExecuteExecutionListenersAsync(flowNode, ContinueProcessOperation.EVENTNAME_END, cancellationToken);

        DispatchActivityCompletedEvent(flowNode);
        RecordActivityEnd();

        var outgoingFlows = flowNode.OutgoingFlows;
        if (outgoingFlows == null || outgoingFlows.Count == 0)
        {
            _agenda.PlanEndExecutionOperation(_execution);
            return;
        }

        string? defaultFlowId = null;
        if (flowNode is BpmnModelNs.Activity activity)
            defaultFlowId = activity.DefaultFlow;
        else if (flowNode is BpmnModelNs.ExclusiveGateway exclusiveGateway)
            defaultFlowId = exclusiveGateway.DefaultFlow;
        else if (flowNode is BpmnModelNs.InclusiveGateway inclusiveGateway)
            defaultFlowId = inclusiveGateway.DefaultFlow;
        else if (flowNode is BpmnModelNs.ComplexGateway complexGateway)
            defaultFlowId = complexGateway.DefaultFlow;

        var flowsToTake = new List<BpmnModelNs.SequenceFlow>();
        BpmnModelNs.SequenceFlow? defaultFlow = null;

        foreach (var sequenceFlow in outgoingFlows)
        {
            if (_evaluateConditions && !string.IsNullOrEmpty(sequenceFlow.ConditionExpression))
            {
                bool conditionResult = EvaluateCondition(sequenceFlow);
                if (conditionResult && (defaultFlowId == null || sequenceFlow.Id != defaultFlowId))
                {
                    flowsToTake.Add(sequenceFlow);
                }
                else if (sequenceFlow.Id == defaultFlowId)
                {
                    defaultFlow = sequenceFlow;
                }
            }
            else
            {
                if (sequenceFlow.Id == defaultFlowId)
                {
                    defaultFlow = sequenceFlow;
                }
                else
                {
                    flowsToTake.Add(sequenceFlow);
                }
            }
        }

        if (flowsToTake.Count == 0 && defaultFlow != null)
        {
            flowsToTake.Add(defaultFlow);
        }

        if (flowsToTake.Count == 0)
        {
            if (outgoingFlows == null || outgoingFlows.Count == 0)
            {
                _agenda.PlanEndExecutionOperation(_execution);
            }
            else
            {
                throw new WorkflowEngineException(
                    $"No outgoing sequence flow of element '{flowNode.Id}' could be selected for continuing the process");
            }
            return;
        }

        if (flowsToTake.Count == 1)
        {
            _execution.IsActive = true;
            SetCurrentFlowElement(_execution, flowsToTake[0]);
            _agenda.PlanContinueProcessOperation(_execution);
        }
        else
        {
            for (int i = 0; i < flowsToTake.Count; i++)
            {
                if (i == 0)
                {
                    _execution.IsActive = true;
                    SetCurrentFlowElement(_execution, flowsToTake[i]);
                    _agenda.PlanContinueProcessOperation(_execution);
                }
                else
                {
                    var childExecution = CreateChildExecution(_execution);
                    SetCurrentFlowElement(childExecution, flowsToTake[i]);
                    childExecution.IsConcurrent = true;
                    _agenda.PlanContinueProcessOperation(childExecution);
                }
            }
        }

        await Task.CompletedTask;
    }

    private bool EvaluateCondition(BpmnModelNs.SequenceFlow sequenceFlow)
    {
        return ConditionUtil.HasTrueCondition(sequenceFlow, _execution, _engineConfig.ExpressionManager);
    }

    private async Task ExecuteExecutionListenersAsync(BpmnModelNs.FlowElement flowElement, string eventName, CancellationToken cancellationToken)
    {
        if (flowElement.ExecutionListeners == null || flowElement.ExecutionListeners.Count == 0)
            return;

        _execution.EventName = eventName;

        foreach (var listener in flowElement.ExecutionListeners)
        {
            if (listener.Event == eventName)
            {
                await ExecuteSingleListenerAsync(listener, cancellationToken);
            }
        }

        _execution.EventName = null;
    }

    private async Task ExecuteSingleListenerAsync(BpmnModelNs.WorkflowExtensionListener listener, CancellationToken cancellationToken)
    {
        if (listener.ImplementationType == "class" && !string.IsNullOrEmpty(listener.Implementation))
        {
            var instance = ClassDelegateUtil.Instantiate(listener.Implementation);
            await ExecuteListenerInstanceAsync(instance, cancellationToken);
        }
        else if (listener.ImplementationType == "delegateExpression" && !string.IsNullOrEmpty(listener.Implementation))
        {
            var instance = DelegateExpressionUtil.ResolveDelegateExpression(
                listener.Implementation,
                _engineConfig.ExpressionManager,
                _execution.Variables);
            await ExecuteListenerInstanceAsync(instance, cancellationToken);
        }
    }

    private async Task ExecuteListenerInstanceAsync(object instance, CancellationToken cancellationToken)
    {
        if (instance is IExecutionListener executionListener)
        {
            await executionListener.NotifyAsync(new DelegateExecution(_execution), cancellationToken);
            return;
        }

        if (instance is IWorkflowDelegate workflowDelegate)
        {
            await workflowDelegate.ExecuteAsync(new DelegateExecution(_execution));
            return;
        }

        if (instance is IWorkflowEventListener eventListener)
        {
            eventListener.OnEvent(new WorkflowEventImplementation(
                WorkflowEventType.CUSTOM,
                _execution.Id,
                _execution.ProcessInstanceId,
                _execution.ProcessDefinitionId));
        }
    }

    private void DispatchActivityCompletedEvent(BpmnModelNs.FlowNode flowNode)
    {
        var eventDispatcher = _engineConfig.EventDispatcher;
        if (eventDispatcher != null)
        {
            var @event = WorkflowEventBuilder.CreateActivityCompletedEvent(
                flowNode.Id ?? "",
                flowNode.GetType().Name,
                _execution.Id,
                _execution.ProcessInstanceId ?? "");
            eventDispatcher.DispatchEvent(@event);
        }
    }

    private void RecordActivityEnd()
    {
        _engineConfig.HistoryManager.RecordActivityEnd(_execution, null);
    }

    private ExecutionEntity CreateChildExecution(ExecutionEntity parent)
    {
        var child = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid(),
            ProcessInstanceId = parent.ProcessInstanceId,
            ProcessDefinitionId = parent.ProcessDefinitionId,
            Parent = parent,
            ParentId = parent.Id,
            IsActive = true,
            IsEnded = false,
            IsScope = false,
            IsConcurrent = true,
            IsProcessInstanceType = false,
            Process = parent.Process,
            Variables = new Dictionary<string, object?>(parent.Variables)
        };
        parent.ChildExecutions.Add(child);
        return child;
    }

    private static void SetCurrentFlowElement(ExecutionEntity execution, BpmnModelNs.FlowElement flowElement)
    {
        execution.CurrentFlowElement = flowElement;
        execution.CurrentFlowElementId = flowElement.Id;
        execution.ActivityId = flowElement.Id;
    }
}


