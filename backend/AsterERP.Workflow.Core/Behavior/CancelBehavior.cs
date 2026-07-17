using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Helper;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class CancelBoundaryEventActivityBehavior : BoundaryEventActivityBehavior
{
    public CancelBoundaryEventActivityBehavior() : base(true) { }

    public CancelBoundaryEventActivityBehavior(bool interrupting) : base(interrupting) { }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_cancelBoundarySubscription", true);
        execution.SetVariableLocal("_cancelBoundaryActive", true);
    }

    public override async Task TriggerAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var subProcessExecution = FindSubProcessExecution(execution);
        if (subProcessExecution != null)
        {
            var compensateSubscriptions = FindCompensateEventSubscriptions(subProcessExecution);

            if (compensateSubscriptions.Count > 0)
            {
                ScopeUtil.ThrowCompensationEvent(compensateSubscriptions, execution, false);
            }

            subProcessExecution.IsActive = false;
            subProcessExecution.IsEnded = true;
            subProcessExecution.SetVariable("_transactionCancelled", true);
            subProcessExecution.SetVariable("_transactionActive", false);
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

    public async Task CancelCallback(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var transactionId = execution.GetVariable("_transactionId") as string;
        if (transactionId != null)
        {
            execution.SetVariableLocal("_transactionCancelled", true);
            execution.SetVariableLocal("_cancelTransactionId", transactionId);
        }

        await LeaveAsync(execution, cancellationToken);
    }

    protected ExecutionEntity? FindSubProcessExecution(ExecutionEntity execution)
    {
        var boundaryEvent = execution.CurrentFlowElement as BpmnModelNs.BoundaryEvent;
        var attachedToRefId = boundaryEvent?.AttachedToRefId;

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

    protected List<CompensateEventSubscription> FindCompensateEventSubscriptions(ExecutionEntity subProcessExecution)
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
}

public class EventSubProcessErrorStartEventActivityBehavior : FlowNodeActivityBehavior
{
    public string? ErrorCode { get; set; }

    public EventSubProcessErrorStartEventActivityBehavior() { }

    public EventSubProcessErrorStartEventActivityBehavior(string? errorCode = null)
    {
        ErrorCode = errorCode;
    }

    public EventSubProcessErrorStartEventActivityBehavior(string? errorCode, object? subProcessBehavior)
    {
        ErrorCode = errorCode;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_errorStartEventTriggered", true);
        execution.SetVariableLocal("_errorCode", ErrorCode);

        var subProcess = FindParentSubProcess(execution);
        if (subProcess != null)
        {
            var childExecution = new ExecutionEntity
            {
                Id = AbpTimeIdProvider.NewGuid(),
                ProcessInstanceId = execution.ProcessInstanceId,
                Parent = execution,
                IsActive = true,
                IsEnded = false,
                CurrentActivityId = execution.CurrentActivityId
            };
            execution.ChildExecutions.Add(childExecution);

            foreach (var dataObject in subProcess.DataObjects)
            {
                childExecution.SetVariable(dataObject.Id, dataObject.Value);
            }
        }

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    public virtual async Task TriggerErrorAsync(ExecutionEntity execution, string? errorCode, CancellationToken cancellationToken = default)
    {
        ErrorCode = errorCode;
        execution.SetVariableLocal("_errorTriggered", true);
        execution.SetVariableLocal("_errorCode", errorCode);

        var subProcess = FindParentSubProcess(execution);
        if (subProcess != null)
        {
            CancelChildExecutions(execution);
        }

        await ExecuteAsync(execution, cancellationToken);
    }

    protected BpmnModelNs.SubProcess? FindParentSubProcess(ExecutionEntity execution)
    {
        if (execution.CurrentFlowElement is BpmnModelNs.StartEvent startEvent && startEvent.ParentContainer is BpmnModelNs.SubProcess subProcess)
        {
            return subProcess;
        }
        return null;
    }

    protected void CancelChildExecutions(ExecutionEntity execution)
    {
        var parent = execution.Parent;
        if (parent != null)
        {
            foreach (var child in parent.ChildExecutions.ToList())
            {
                if (child.Id != execution.Id && child.IsActive)
                {
                    child.IsActive = false;
                    child.IsEnded = true;
                }
            }
        }
    }
}

public class EventSubProcessMessageStartEventActivityBehavior : FlowNodeActivityBehavior, ITriggerableActivityBehavior
{
    public BpmnModelNs.MessageEventDefinition? MessageEventDefinition { get; set; }
    public string? MessageName { get; set; }

    public EventSubProcessMessageStartEventActivityBehavior() { }

    public EventSubProcessMessageStartEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition? messageEventDefinition = null,
        string? messageName = null)
    {
        MessageEventDefinition = messageEventDefinition;
        MessageName = messageName;
    }

    public EventSubProcessMessageStartEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition? messageEventDefinition,
        string? messageName,
        object? subProcessBehavior)
    {
        MessageEventDefinition = messageEventDefinition;
        MessageName = messageName;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var resolvedMessageName = ResolveMessageName(execution);
        if (resolvedMessageName == null)
        {
            return;
        }

        execution.SetVariable("_messageSubscription_" + resolvedMessageName, true);
        execution.SetVariableLocal("_messageSubscriptionName", resolvedMessageName);
        execution.SetVariableLocal("_messageSubscriptionActive", true);
        execution.IsActive = false;
    }

    public virtual async Task TriggerMessageAsync(ExecutionEntity execution, string? messageName, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var resolvedMessageName = messageName ?? ResolveMessageName(execution);
        MessageName = resolvedMessageName;
        execution.SetVariableLocal("_messageTriggered", true);
        execution.SetVariableLocal("_messageName", resolvedMessageName);

        var subscriptionName = execution.GetVariableLocal("_messageSubscriptionName") as string;
        if (subscriptionName != null)
        {
            execution.RemoveVariable("_messageSubscription_" + subscriptionName);
        }
        execution.SetVariableLocal("_messageSubscriptionActive", false);

        var targetExecution = FindParentScopeExecution(execution.Parent ?? execution) ?? execution;
        PropagatePayloadVariables(execution, targetExecution);

        if (variables != null)
        {
            foreach (var kv in variables)
            {
                targetExecution.SetVariable(kv.Key, kv.Value);
            }
        }

        InterruptEnclosingScope(execution);

        execution.IsActive = true;
        execution.IsEnded = false;

        if (execution.CurrentFlowElement != null)
        {
            await LeaveAsync(execution, cancellationToken);
        }
    }

    public virtual async Task TriggerAsync(ExecutionEntity execution, string? signalName, object? signalData, CancellationToken cancellationToken = default)
    {
        Dictionary<string, object?>? variables = signalData as Dictionary<string, object?>;

        if (signalData != null && variables == null)
        {
            variables = new Dictionary<string, object?> { ["_messageData"] = signalData };
        }

        await TriggerMessageAsync(execution, signalName, variables, cancellationToken);
    }

    protected string? ResolveMessageName(ExecutionEntity execution)
    {
        if (!string.IsNullOrWhiteSpace(MessageName))
        {
            return MessageName;
        }

        return MessageEventDefinition?.MessageRef;
    }

    protected ExecutionEntity? FindParentScopeExecution(ExecutionEntity execution)
    {
        var current = execution;
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

    protected void InterruptEnclosingScope(ExecutionEntity execution)
    {
        var parentScopeExecution = FindParentScopeExecution(execution.Parent ?? execution);
        if (parentScopeExecution == null)
        {
            return;
        }

        foreach (var child in parentScopeExecution.ChildExecutions.ToList())
        {
            if (child.Id == execution.Id)
            {
                continue;
            }

            child.IsActive = false;
            child.IsEnded = true;
        }

        if (parentScopeExecution.Id != execution.Id)
        {
            parentScopeExecution.IsActive = false;
        }
    }

    protected void PropagatePayloadVariables(ExecutionEntity sourceExecution, ExecutionEntity targetExecution)
    {
        if (ReferenceEquals(sourceExecution, targetExecution))
        {
            return;
        }

        foreach (var variable in sourceExecution.Variables)
        {
            if (string.IsNullOrEmpty(variable.Key) || variable.Key.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            targetExecution.SetVariable(variable.Key, variable.Value);
        }
    }
}

public class IntermediateMessageThrowEventActivityBehavior : FlowNodeActivityBehavior
{
    public BpmnModelNs.MessageEventDefinition? MessageEventDefinition { get; set; }
    public string? MessageName { get; set; }

    protected IExpressionManager? ExpressionManager { get; set; }

    public IntermediateMessageThrowEventActivityBehavior() { }

    public IntermediateMessageThrowEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition? messageEventDefinition = null,
        string? messageName = null,
        IExpressionManager? expressionManager = null)
    {
        MessageEventDefinition = messageEventDefinition;
        MessageName = messageName;
        ExpressionManager = expressionManager;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var resolvedMessageName = ResolveMessageName(execution);

        execution.SetVariable("_thrownMessageName", resolvedMessageName);
        execution.SetVariable("_thrownMessageTimestamp", AbpTimeIdProvider.UtcNow);

        if (!string.IsNullOrEmpty(resolvedMessageName))
        {
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
        }

        await LeaveAsync(execution, cancellationToken);
    }

    protected List<ExecutionEntity> FindWaitingExecutions(ExecutionEntity sourceExecution, string messageName)
    {
        var results = new List<ExecutionEntity>();
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


