using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Helper;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Agenda;

public class ContinueProcessOperation
{
    public const string EVENTNAME_START = "start";
    public const string EVENTNAME_END = "end";
    public const string EVENTNAME_TAKE = "take";

    private readonly IAgenda _agenda;
    private readonly ExecutionEntity _execution;
    private readonly IProcessEngineConfiguration _engineConfig;
    private readonly bool _forceSynchronousOperation;
    private readonly bool _inCompensation;

    public ContinueProcessOperation(IAgenda agenda, ExecutionEntity execution, IProcessEngineConfiguration engineConfig)
        : this(agenda, execution, engineConfig, false, false)
    {
    }

    public ContinueProcessOperation(IAgenda agenda, ExecutionEntity execution, IProcessEngineConfiguration engineConfig, bool forceSynchronousOperation, bool inCompensation)
    {
        _agenda = agenda;
        _execution = execution;
        _engineConfig = engineConfig;
        _forceSynchronousOperation = forceSynchronousOperation;
        _inCompensation = inCompensation;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var currentFlowElement = _execution.CurrentFlowElement;
        if (currentFlowElement == null)
            throw new WorkflowEngineException($"No current flow element found for execution {_execution.Id}");

        if (currentFlowElement is BpmnModelNs.FlowNode flowNode)
            await ContinueThroughFlowNode(flowNode, cancellationToken);
        else if (currentFlowElement is BpmnModelNs.SequenceFlow sequenceFlow)
            await ContinueThroughSequenceFlow(sequenceFlow, cancellationToken);
        else
            throw new WorkflowEngineException($"Unsupported flow element type: {currentFlowElement.GetType().Name}");
    }

    private async Task ContinueThroughFlowNode(BpmnModelNs.FlowNode flowNode, CancellationToken cancellationToken)
    {
        if (flowNode is BpmnModelNs.Transaction transaction && !IsMultiInstance(transaction))
        {
            await ContinueThroughSubProcess(transaction, cancellationToken);
        }
        else if (flowNode is BpmnModelNs.SubProcess subProcess && !IsMultiInstance(subProcess))
        {
            await ContinueThroughSubProcess(subProcess, cancellationToken);
        }
        else if (IsMultiInstance(flowNode))
        {
            await ContinueThroughMultiInstance(flowNode, cancellationToken);
        }
        else if (_forceSynchronousOperation || !flowNode.Asynchronous)
        {
            await ExecuteSynchronous(flowNode, cancellationToken);
        }
        else
        {
            await ExecuteAsynchronous(flowNode, cancellationToken);
        }
    }

    private async Task ExecuteSynchronous(BpmnModelNs.FlowNode flowNode, CancellationToken cancellationToken)
    {
        await ExecuteExecutionListenersAsync(flowNode, EVENTNAME_START, cancellationToken);

        ExecuteBoundaryEvents(flowNode);
        InitializeTriggeredEventSubProcesses(flowNode);

        DispatchActivityStartedEvent(flowNode);
        RecordActivityStart();

        var existingChildCount = _execution.ChildExecutions.Count;
        var existingTaskIds = _execution.TaskEntities
            .Select(task => task.Id)
            .ToHashSet(StringComparer.Ordinal);

        var behavior = flowNode.Behavior as IBpmnActivityBehavior;
        var elementBeforeExecution = _execution.CurrentFlowElement;
        if (behavior is FlowNodeActivityBehavior flowNodeBehavior)
        {
            flowNodeBehavior.Agenda = _agenda;
        }
        if (behavior != null)
        {
            await behavior.ExecuteAsync(_execution, cancellationToken);
        }
        else
        {
            _agenda.PlanTakeOutgoingSequenceFlowsOperation(_execution, true);
        }

        foreach (var task in _execution.TaskEntities.Where(task => existingTaskIds.Add(task.Id)))
        {
            _engineConfig.HistoryManager.RecordTaskCreated(
                _execution,
                task.Id,
                task.Name ?? task.TaskDefinitionKey ?? task.Id,
                task.Assignee);
        }

        if (_execution.IsEnded)
        {
            RecordActivityEnd();
            _agenda.PlanEndExecutionOperation(_execution);
            return;
        }

        if (_execution.CurrentFlowElement is BpmnModelNs.SequenceFlow)
        {
            _agenda.PlanContinueProcessOperation(_execution);
        }
        else if (_execution.CurrentFlowElement is BpmnModelNs.FlowNode newFlowNode && newFlowNode != flowNode)
        {
            _agenda.PlanContinueProcessOperation(_execution);
        }

        for (int i = existingChildCount; i < _execution.ChildExecutions.Count; i++)
        {
            var childExecution = _execution.ChildExecutions[i];
            if (childExecution.IsActive && !childExecution.IsEnded && childExecution.CurrentFlowElement != null)
            {
                if (!(_execution.CurrentFlowElement is BpmnModelNs.SubProcess) &&
                    !IsStartedCallActivityChildProcess(childExecution))
                {
                    _agenda.PlanContinueProcessOperation(childExecution);
                }
            }
        }
    }

    private async Task ContinueThroughSubProcess(BpmnModelNs.SubProcess subProcess, CancellationToken cancellationToken)
    {
        await ExecuteExecutionListenersAsync(subProcess, EVENTNAME_START, cancellationToken);
        DispatchActivityStartedEvent(subProcess);
        RecordActivityStart();

        var childExecution = CreateChildExecution(_execution);
        childExecution.IsScope = true;
        childExecution.CurrentFlowElement = subProcess;

        var startElement = FindStartElement(subProcess);
        if (startElement != null)
        {
            childExecution.CurrentFlowElement = startElement;
            _agenda.PlanContinueProcessOperation(childExecution);
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteAsynchronous(BpmnModelNs.FlowNode flowNode, CancellationToken cancellationToken)
    {
        var jobManager = _engineConfig.JobManager;
        if (jobManager != null)
        {
            var job = await jobManager.CreateAsyncJobAsync(
                _execution.Id,
                _execution.ProcessInstanceId!,
                _execution.ProcessDefinitionId!,
                flowNode.Exclusive,
                cancellationToken);
            if (job != null)
            {
                await jobManager.ScheduleAsyncJobAsync(job, cancellationToken);
            }
        }
    }

    private async Task ContinueThroughMultiInstance(BpmnModelNs.FlowNode flowNode, CancellationToken cancellationToken)
    {
        await ExecuteExecutionListenersAsync(flowNode, EVENTNAME_START, cancellationToken);

        if (!_inCompensation && flowNode is BpmnModelNs.Activity activity && activity.BoundaryEvents != null && activity.BoundaryEvents.Count > 0)
        {
            ExecuteBoundaryEvents(flowNode);
        }

        DispatchActivityStartedEvent(flowNode);
        RecordActivityStart();

        var behavior = flowNode.Behavior as IBpmnActivityBehavior;
        if (behavior is FlowNodeActivityBehavior flowNodeBehavior2)
        {
            flowNodeBehavior2.Agenda = _agenda;
        }
        if (behavior != null)
        {
            await behavior.ExecuteAsync(_execution, cancellationToken);
        }
        else
        {
            throw new WorkflowEngineException($"Expected an activity behavior in flow node {flowNode.Id}");
        }
    }

    private async Task ContinueThroughSequenceFlow(BpmnModelNs.SequenceFlow sequenceFlow, CancellationToken cancellationToken)
    {
        await ExecuteExecutionListenersAsync(sequenceFlow, EVENTNAME_TAKE, cancellationToken);

        DispatchSequenceFlowTakenEvent(sequenceFlow);

        var targetFlowElement = sequenceFlow.TargetFlowElement;
        if (targetFlowElement == null && !string.IsNullOrEmpty(sequenceFlow.TargetRef))
        {
            var process = _execution.Process;
            if (process != null)
            {
                targetFlowElement = process.FlowElements.FirstOrDefault(e => e.Id == sequenceFlow.TargetRef) as BpmnModelNs.FlowNode;
            }
        }

        if (targetFlowElement != null)
        {
            _execution.CurrentFlowElement = targetFlowElement;
            _execution.CurrentFlowElementId = targetFlowElement.Id;
            _execution.ActivityId = targetFlowElement.Id;
            _agenda.PlanContinueProcessOperation(_execution);
        }

        await Task.CompletedTask;
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

    private void ExecuteBoundaryEvents(BpmnModelNs.FlowNode flowNode)
    {
        if (flowNode is BpmnModelNs.Activity activity && activity.BoundaryEvents != null)
        {
            foreach (var boundaryEvent in activity.BoundaryEvents)
            {
                var childExecution = CreateChildExecution(_execution);
                childExecution.CurrentFlowElement = boundaryEvent;
                childExecution.CurrentFlowElementId = boundaryEvent.Id;
                childExecution.ActivityId = boundaryEvent.Id;
                _agenda.PlanContinueProcessOperation(childExecution);
            }
        }
    }

    private void DispatchActivityStartedEvent(BpmnModelNs.FlowNode flowNode)
    {
        var eventDispatcher = _engineConfig.EventDispatcher;
        if (eventDispatcher != null)
        {
            var @event = WorkflowEventBuilder.CreateActivityStartedEvent(
                flowNode.Id ?? "",
                flowNode.GetType().Name,
                _execution.Id,
                _execution.ProcessInstanceId ?? "");
            eventDispatcher.DispatchEvent(@event);
        }
    }

    private void RecordActivityStart()
    {
        _engineConfig.HistoryManager.RecordActivityStart(_execution);
    }

    private void RecordActivityEnd()
    {
        _engineConfig.HistoryManager.RecordActivityEnd(_execution, null);
    }

    private void DispatchSequenceFlowTakenEvent(BpmnModelNs.SequenceFlow sequenceFlow)
    {
        var eventDispatcher = _engineConfig.EventDispatcher;
        if (eventDispatcher != null)
        {
            var @event = WorkflowEventBuilder.CreateSequenceFlowTakenEvent(
                sequenceFlow.Id ?? "",
                sequenceFlow.SourceRef,
                sequenceFlow.TargetRef,
                _execution.Id,
                _execution.ProcessInstanceId ?? "");
            eventDispatcher.DispatchEvent(@event);
        }
    }

    private bool IsMultiInstance(BpmnModelNs.FlowNode flowNode)
    {
        if (flowNode is BpmnModelNs.Activity activity)
            return activity.LoopCharacteristics != null;
        return false;
    }

    private BpmnModelNs.FlowElement? FindStartElement(BpmnModelNs.SubProcess subProcess)
    {
        foreach (var flowElement in subProcess.FlowElements)
        {
            if (flowElement is BpmnModelNs.StartEvent)
                return flowElement;
        }
        return null;
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
            IsConcurrent = false,
            IsProcessInstanceType = false,
            Process = parent.Process,
            Variables = new Dictionary<string, object?>(parent.Variables)
        };
        parent.ChildExecutions.Add(child);
        return child;
    }

    private void InitializeTriggeredEventSubProcesses(BpmnModelNs.FlowNode flowNode)
    {
        if (!_execution.IsScope || Equals(_execution.GetVariableLocal("_eventSubProcessSubscriptionsInitialized"), true))
        {
            return;
        }

        var scopeContainer = ResolveScopeContainer(flowNode);
        if (scopeContainer == null)
        {
            return;
        }

        foreach (var flowElement in scopeContainer.FlowElements)
        {
            if (flowElement is not BpmnModelNs.SubProcess { TriggeredByEvent: true } eventSubProcess)
            {
                continue;
            }

            foreach (var childElement in eventSubProcess.FlowElements)
            {
                if (childElement is not BpmnModelNs.StartEvent startEvent || startEvent.Behavior == null)
                {
                    continue;
                }

                var childExecution = CreateChildExecution(_execution);
                childExecution.CurrentFlowElement = startEvent;
                childExecution.CurrentFlowElementId = startEvent.Id;
                childExecution.ActivityId = startEvent.Id;
                _agenda.PlanContinueProcessOperation(childExecution);
            }
        }

        _execution.SetVariableLocal("_eventSubProcessSubscriptionsInitialized", true);
    }

    private BpmnModelNs.IFlowElementsContainer? ResolveScopeContainer(BpmnModelNs.FlowNode flowNode)
    {
        if (flowNode.ParentContainer != null)
        {
            return flowNode.ParentContainer;
        }

        return _execution.Process;
    }

    private bool IsStartedCallActivityChildProcess(ExecutionEntity childExecution)
    {
        return childExecution.IsProcessInstanceType
            && !string.IsNullOrEmpty(childExecution.SuperExecutionId)
            && string.Equals(childExecution.ParentId, _execution.Id, StringComparison.Ordinal);
    }
}


