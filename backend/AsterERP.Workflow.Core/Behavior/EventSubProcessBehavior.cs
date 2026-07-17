using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Helper;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class BoundaryCancelEventActivityBehavior : BoundaryEventActivityBehavior
{
    protected BpmnModelNs.CancelEventDefinition? CancelEventDefinition { get; set; }

    public BoundaryCancelEventActivityBehavior() : base(true) { }

    public BoundaryCancelEventActivityBehavior(
        BpmnModelNs.CancelEventDefinition? cancelEventDefinition = null,
        bool interrupting = true)
        : base(interrupting)
    {
        CancelEventDefinition = cancelEventDefinition;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_cancelBoundarySubscription", true);
        execution.SetVariableLocal("_cancelBoundaryActive", true);
    }

    public override async Task TriggerAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var boundaryEvent = execution.CurrentFlowElement as BpmnModelNs.BoundaryEvent;
        var attachedToRefId = boundaryEvent?.AttachedToRefId;

        var subProcessExecution = FindSubProcessExecution(execution, attachedToRefId);
        if (subProcessExecution != null)
        {
            var compensateSubscriptions = FindCompensateEventSubscriptions(subProcessExecution);

            if (compensateSubscriptions.Count == 0)
            {
                await LeaveAsync(execution, cancellationToken);
            }
            else
            {
                var deleteReason = "boundaryEventInterrupting" + "(" + (boundaryEvent?.Id ?? "") + ")";
                ScopeUtil.ThrowCompensationEvent(compensateSubscriptions, execution, false);

                subProcessExecution.IsActive = false;
                subProcessExecution.IsEnded = true;
                subProcessExecution.SetVariable("_transactionCancelled", true);
                subProcessExecution.SetVariable("_transactionActive", false);

                if (Interrupting)
                {
                    await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
                }
                else
                {
                    await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
                }
            }
        }
        else
        {
            var transactionExecution = FindParentTransactionExecution(execution);
            if (transactionExecution != null)
            {
                transactionExecution.SetVariable("_transactionCancelled", true);
                transactionExecution.SetVariable("_transactionActive", false);
                execution.SetVariableLocal("_transactionCancelled", true);
            }

            if (Interrupting)
            {
                await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
            }
            else
            {
                await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
            }
        }
    }

    protected virtual ExecutionEntity? FindSubProcessExecution(ExecutionEntity execution, string? attachedToRefId)
    {
        if (string.IsNullOrEmpty(attachedToRefId)) return null;

        var root = FindRootExecution(execution);
        return FindExecutionByActivityId(root, attachedToRefId);
    }

    protected ExecutionEntity FindRootExecution(ExecutionEntity execution)
    {
        var current = execution;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    protected ExecutionEntity? FindExecutionByActivityId(ExecutionEntity execution, string activityId)
    {
        if (execution.CurrentActivityId == activityId && execution.IsActive)
        {
            return execution;
        }

        foreach (var child in execution.ChildExecutions)
        {
            var found = FindExecutionByActivityId(child, activityId);
            if (found != null) return found;
        }

        return null;
    }

    protected virtual List<CompensateEventSubscription> FindCompensateEventSubscriptions(ExecutionEntity subProcessExecution)
    {
        var subscriptions = new List<CompensateEventSubscription>();

        if (subProcessExecution.Parent != null)
        {
            foreach (var child in subProcessExecution.Parent.ChildExecutions)
            {
                var hasCompensation = child.GetVariable("_compensationSubscription") as bool?;
                if (hasCompensation == true)
                {
                    subscriptions.Add(new CompensateEventSubscription
                    {
                        Id = child.Id,
                        ExecutionId = child.Id,
                        ActivityId = child.CurrentActivityId,
                        EventType = "compensate",
                        ProcessInstanceId = child.ProcessInstanceId
                    });
                }
            }
        }

        return subscriptions;
    }

    protected virtual ExecutionEntity? FindParentTransactionExecution(ExecutionEntity execution)
    {
        var current = execution.Parent;
        while (current != null)
        {
            var transactionStarted = current.GetVariable("_transactionStarted");
            if (transactionStarted is bool boolVal && boolVal)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }
}

public abstract class AbstractThrowMessageEventActivityBehavior : FlowNodeActivityBehavior
{
    public BpmnModelNs.MessageEventDefinition? MessageEventDefinition { get; set; }
    public string? MessageName { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }

    protected AbstractThrowMessageEventActivityBehavior() { }

    protected AbstractThrowMessageEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition? messageEventDefinition = null,
        string? messageName = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
    {
        MessageEventDefinition = messageEventDefinition;
        MessageName = messageName;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var resolvedMessageName = ResolveMessageName(execution);

        execution.SetVariable("_thrownMessageName", resolvedMessageName);
        execution.SetVariable("_thrownMessageTimestamp", AbpTimeIdProvider.UtcNow);

        var waitingExecutions = FindWaitingExecutions(execution, resolvedMessageName);
        foreach (var waitingExecution in waitingExecutions)
        {
            var messageVariables = execution.Variables != null && execution.Variables.Count > 0
                ? new Dictionary<string, object?>(execution.Variables)
                : null;

            if (messageVariables != null)
            {
                foreach (var kv in messageVariables)
                {
                    waitingExecution.SetVariable(kv.Key, kv.Value);
                }
            }

            waitingExecution.SetVariableLocal("_messageSubscriptionActive", false);
            waitingExecution.SetVariable("_caughtMessageName", resolvedMessageName);
        }

        if (EventDispatcher != null && EventDispatcher.IsEnabled && !string.IsNullOrEmpty(resolvedMessageName))
        {
            EventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateCustomEvent(
                "messageReceived",
                new Dictionary<string, object?>
                {
                    { "messageName", resolvedMessageName },
                    { "activityId", execution.CurrentActivityId },
                    { "processInstanceId", execution.ProcessInstanceId }
                }));
        }

        await ExecuteThrowMessageAsync(execution, resolvedMessageName, cancellationToken);
    }

    protected List<ExecutionEntity> FindWaitingExecutions(ExecutionEntity sourceExecution, string? messageName)
    {
        var results = new List<ExecutionEntity>();
        if (string.IsNullOrEmpty(messageName)) return results;

        var rootExecution = FindRootExecution(sourceExecution);
        FindWaitingExecutionsInTree(rootExecution, messageName, sourceExecution.Id, results);
        return results;
    }

    protected void FindWaitingExecutionsInTree(ExecutionEntity execution, string messageName, string excludeExecutionId, List<ExecutionEntity> results)
    {
        var subscriptionActive = execution.GetVariableLocal("_messageSubscriptionActive") as bool?;
        var subscriptionName = execution.GetVariableLocal("_messageSubscriptionName") as string;

        if (subscriptionActive == true && subscriptionName == messageName && execution.Id != excludeExecutionId)
        {
            results.Add(execution);
        }

        foreach (var child in execution.ChildExecutions)
        {
            FindWaitingExecutionsInTree(child, messageName, excludeExecutionId, results);
        }
    }

    protected ExecutionEntity FindRootExecution(ExecutionEntity execution)
    {
        var current = execution;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    protected abstract Task ExecuteThrowMessageAsync(
        ExecutionEntity execution,
        string? messageName,
        CancellationToken cancellationToken);

    protected string? ResolveMessageName(ExecutionEntity execution)
    {
        if (!string.IsNullOrEmpty(MessageName))
        {
            if (ExpressionManager != null && (MessageName.StartsWith("${") || MessageName.StartsWith("#{")))
            {
                var result = ExpressionManager.Evaluate(MessageName, execution.Variables);
                return result?.ToString();
            }
            return MessageName;
        }

        if (MessageEventDefinition != null && !string.IsNullOrEmpty(MessageEventDefinition.MessageRef))
        {
            return MessageEventDefinition.MessageRef;
        }

        return null;
    }
}

public class IntermediateThrowMessageEventActivityBehavior : AbstractThrowMessageEventActivityBehavior
{
    public IntermediateThrowMessageEventActivityBehavior() { }

    public IntermediateThrowMessageEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition? messageEventDefinition = null,
        string? messageName = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
        : base(messageEventDefinition, messageName, expressionManager, eventDispatcher)
    {
    }

    protected override async Task ExecuteThrowMessageAsync(
        ExecutionEntity execution,
        string? messageName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(messageName))
        {
            execution.SetVariable("_messageThrown_" + messageName, true);
            execution.EventName = messageName;
        }

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }
}

public class ThrowMessageEndEventActivityBehavior : AbstractThrowMessageEventActivityBehavior
{
    public ThrowMessageEndEventActivityBehavior() { }

    public ThrowMessageEndEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition? messageEventDefinition = null,
        string? messageName = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
        : base(messageEventDefinition, messageName, expressionManager, eventDispatcher)
    {
    }

    protected override async Task ExecuteThrowMessageAsync(
        ExecutionEntity execution,
        string? messageName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(messageName))
        {
            execution.SetVariable("_messageThrown_" + messageName, true);
            execution.EventName = messageName;
        }

        execution.IsActive = false;
        execution.IsEnded = true;
    }
}

public class EnhancedBoundarySignalEventActivityBehavior : BoundaryEventActivityBehavior
{
    protected BpmnModelNs.SignalEventDefinition? SignalEventDefinition { get; set; }
    protected BpmnModelNs.Signal? Signal { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }
    protected bool IsAsync { get; set; }
    protected string? CorrelationKey { get; set; }

    public EnhancedBoundarySignalEventActivityBehavior() { }

    public EnhancedBoundarySignalEventActivityBehavior(
        BpmnModelNs.SignalEventDefinition signalEventDefinition,
        BpmnModelNs.Signal? signal,
        bool interrupting,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null,
        bool isAsync = false,
        string? correlationKey = null)
        : base(interrupting)
    {
        SignalEventDefinition = signalEventDefinition;
        Signal = signal;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
        IsAsync = isAsync;
        CorrelationKey = correlationKey;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var signalName = ResolveSignalName(execution);
        if (signalName != null)
        {
            execution.SetVariable("_signalSubscription_" + signalName, true);
            execution.SetVariableLocal("_signalSubscriptionActive", true);
            execution.SetVariableLocal("_signalSubscriptionName", signalName);

            if (!string.IsNullOrEmpty(CorrelationKey) && ExpressionManager != null)
            {
                var correlationResult = ExpressionManager.Evaluate(CorrelationKey, execution.Variables);
                if (correlationResult != null)
                {
                    execution.SetVariableLocal("_signalCorrelationKey", correlationResult.ToString());
                }
            }

            if (EventDispatcher != null && EventDispatcher.IsEnabled)
            {
                EventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateCustomEvent(
                    "signalWaiting",
                    new Dictionary<string, object?>
                    {
                        { "signalName", signalName },
                        { "activityId", execution.CurrentActivityId }
                    }));
            }
        }
    }

    public virtual async Task TriggerAsync(ExecutionEntity execution, string? signalName = null, object? signalData = null, CancellationToken cancellationToken = default)
    {
        if (CancelActivity)
        {
            var subscriptionName = execution.GetVariableLocal("_signalSubscriptionName") as string;
            if (subscriptionName != null)
            {
                execution.RemoveVariable("_signalSubscription_" + subscriptionName);
            }
        }
        execution.SetVariableLocal("_signalSubscriptionActive", false);

        if (signalData != null)
        {
            execution.SetVariable("_signalData", signalData);
        }

        if (!string.IsNullOrEmpty(signalName))
        {
            execution.SetVariable("_caughtSignalName", signalName);
        }

        if (Interrupting)
        {
            await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
        }
        else
        {
            await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
        }
    }

    protected string? ResolveSignalName(ExecutionEntity execution)
    {
        if (SignalEventDefinition == null) return null;

        if (!string.IsNullOrEmpty(SignalEventDefinition.SignalRef))
        {
            return SignalEventDefinition.SignalRef;
        }

        if (ExpressionManager != null && Signal != null && !string.IsNullOrEmpty(Signal.Name))
        {
            var result = ExpressionManager.Evaluate(Signal.Name, execution.Variables);
            return result?.ToString();
        }

        return Signal?.Name;
    }
}

public class EnhancedBoundaryMessageEventActivityBehavior : BoundaryEventActivityBehavior
{
    protected BpmnModelNs.MessageEventDefinition? MessageEventDefinition { get; set; }
    protected BpmnModelNs.Message? Message { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }
    protected bool IsAsync { get; set; }
    protected string? CorrelationKey { get; set; }

    public EnhancedBoundaryMessageEventActivityBehavior() { }

    public EnhancedBoundaryMessageEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition messageEventDefinition,
        bool interrupting,
        BpmnModelNs.Message? message = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null,
        bool isAsync = false,
        string? correlationKey = null)
        : base(interrupting)
    {
        MessageEventDefinition = messageEventDefinition;
        Message = message;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
        IsAsync = isAsync;
        CorrelationKey = correlationKey;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var messageName = ResolveMessageName(execution);
        if (messageName != null)
        {
            execution.SetVariable("_messageSubscription_" + messageName, true);
            execution.SetVariableLocal("_messageSubscriptionActive", true);
            execution.SetVariableLocal("_messageSubscriptionName", messageName);

            if (!string.IsNullOrEmpty(CorrelationKey) && ExpressionManager != null)
            {
                var correlationResult = ExpressionManager.Evaluate(CorrelationKey, execution.Variables);
                if (correlationResult != null)
                {
                    execution.SetVariableLocal("_messageCorrelationKey", correlationResult.ToString());
                }
            }

            if (EventDispatcher != null && EventDispatcher.IsEnabled)
            {
                EventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateCustomEvent(
                    "messageWaiting",
                    new Dictionary<string, object?>
                    {
                        { "messageName", messageName },
                        { "activityId", execution.CurrentActivityId }
                    }));
            }
        }
    }

    public virtual async Task TriggerAsync(ExecutionEntity execution, string? messageName = null, object? messageData = null, CancellationToken cancellationToken = default)
    {
        if (CancelActivity)
        {
            var subscriptionName = execution.GetVariableLocal("_messageSubscriptionName") as string;
            if (subscriptionName != null)
            {
                execution.RemoveVariable("_messageSubscription_" + subscriptionName);
            }
        }
        execution.SetVariableLocal("_messageSubscriptionActive", false);

        if (messageData != null)
        {
            execution.SetVariable("_messageData", messageData);
        }

        if (!string.IsNullOrEmpty(messageName))
        {
            execution.SetVariable("_caughtMessageName", messageName);
        }

        if (Interrupting)
        {
            await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
        }
        else
        {
            await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
        }
    }

    protected string? ResolveMessageName(ExecutionEntity execution)
    {
        if (MessageEventDefinition == null) return null;

        if (!string.IsNullOrEmpty(MessageEventDefinition.MessageRef))
        {
            return MessageEventDefinition.MessageRef;
        }

        if (Message != null && !string.IsNullOrEmpty(Message.Name))
        {
            if (ExpressionManager != null)
            {
                var result = ExpressionManager.Evaluate(Message.Name, execution.Variables);
                return result?.ToString();
            }
            return Message.Name;
        }

        return null;
    }
}

