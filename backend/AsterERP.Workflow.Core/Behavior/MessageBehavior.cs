using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class MessageEventCatchBehavior : FlowNodeActivityBehavior, ITriggerableActivityBehavior
{
    protected BpmnModelNs.MessageEventDefinition? MessageEventDefinition { get; set; }
    protected BpmnModelNs.Message? Message { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }

    public MessageEventCatchBehavior() { }

    public MessageEventCatchBehavior(
        BpmnModelNs.MessageEventDefinition messageEventDefinition,
        BpmnModelNs.Message? message = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
    {
        MessageEventDefinition = messageEventDefinition;
        Message = message;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var messageName = ResolveMessageName(execution);
        if (messageName != null)
        {
            execution.SetVariable("_messageSubscription_" + messageName, true);
            execution.SetVariableLocal("_messageSubscriptionName", messageName);
            execution.SetVariableLocal("_messageSubscriptionActive", true);
            execution.IsActive = false;

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

    public virtual async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, string? messageName = null, object? messageData = null, CancellationToken cancellationToken = default)
    {
        DeleteMessageEventSubscription(execution);

        if (messageData != null)
        {
            execution.SetVariable("_messageData", messageData);
        }

        if (!string.IsNullOrEmpty(messageName))
        {
            execution.SetVariable("_caughtMessageName", messageName);
        }

        execution.IsActive = true;
        await LeaveAsync(execution, cancellationToken);
    }

    protected void DeleteMessageEventSubscription(ExecutionEntity execution)
    {
        var subscriptionName = execution.GetVariableLocal("_messageSubscriptionName") as string;
        if (subscriptionName != null)
        {
            execution.RemoveVariable("_messageSubscription_" + subscriptionName);
        }
        execution.SetVariableLocal("_messageSubscriptionActive", false);
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

public class MessageStartEventActivityBehavior : FlowNodeActivityBehavior
{
    protected BpmnModelNs.MessageEventDefinition? MessageEventDefinition { get; set; }
    protected BpmnModelNs.Message? Message { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }

    public MessageStartEventActivityBehavior() { }

    public MessageStartEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition messageEventDefinition,
        BpmnModelNs.Message? message = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
    {
        MessageEventDefinition = messageEventDefinition;
        Message = message;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var messageName = ResolveMessageName(execution);
        if (messageName != null)
        {
            execution.SetVariable("_messageSubscription_" + messageName, true);
            execution.SetVariableLocal("_messageSubscriptionName", messageName);
        }
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
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

public class MessageBoundaryEventActivityBehavior : BoundaryEventActivityBehavior, ITriggerableActivityBehavior
{
    protected BpmnModelNs.MessageEventDefinition? MessageEventDefinition { get; set; }
    protected BpmnModelNs.Message? Message { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }

    public MessageBoundaryEventActivityBehavior() { }

    public MessageBoundaryEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition messageEventDefinition,
        bool interrupting,
        BpmnModelNs.Message? message = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
        : base(interrupting)
    {
        MessageEventDefinition = messageEventDefinition;
        Message = message;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var messageName = ResolveMessageName(execution);
        if (messageName != null)
        {
            execution.SetVariable("_messageSubscription_" + messageName, true);
            execution.SetVariableLocal("_messageSubscriptionName", messageName);
            execution.SetVariableLocal("_messageSubscriptionActive", true);

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

    public virtual async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, string? messageName = null, object? messageData = null, CancellationToken cancellationToken = default)
    {
        if (CancelActivity)
        {
            DeleteMessageEventSubscription(execution);
        }

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

    protected void DeleteMessageEventSubscription(ExecutionEntity execution)
    {
        var subscriptionName = execution.GetVariableLocal("_messageSubscriptionName") as string;
        if (subscriptionName != null)
        {
            execution.RemoveVariable("_messageSubscription_" + subscriptionName);
        }
        execution.SetVariableLocal("_messageSubscriptionActive", false);
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

public class IntermediateMessageCatchEventActivityBehavior : FlowNodeActivityBehavior, ITriggerableActivityBehavior
{
    protected BpmnModelNs.MessageEventDefinition? MessageEventDefinition { get; set; }
    protected BpmnModelNs.Message? Message { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }

    public IntermediateMessageCatchEventActivityBehavior() { }

    public IntermediateMessageCatchEventActivityBehavior(
        BpmnModelNs.MessageEventDefinition messageEventDefinition,
        BpmnModelNs.Message? message = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
    {
        MessageEventDefinition = messageEventDefinition;
        Message = message;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var messageName = ResolveMessageName(execution);
        if (messageName != null)
        {
            execution.SetVariable("_messageSubscription_" + messageName, true);
            execution.SetVariableLocal("_messageSubscriptionName", messageName);
            execution.SetVariableLocal("_messageSubscriptionActive", true);
            execution.IsActive = false;

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

    public virtual async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, string? messageName = null, object? messageData = null, CancellationToken cancellationToken = default)
    {
        DeleteMessageEventSubscription(execution);

        if (messageData != null)
        {
            execution.SetVariable("_messageData", messageData);
        }

        if (!string.IsNullOrEmpty(messageName))
        {
            execution.SetVariable("_caughtMessageName", messageName);
        }

        execution.IsActive = true;
        await LeaveAsync(execution, cancellationToken);
    }

    protected void DeleteMessageEventSubscription(ExecutionEntity execution)
    {
        var subscriptionName = execution.GetVariableLocal("_messageSubscriptionName") as string;
        if (subscriptionName != null)
        {
            execution.RemoveVariable("_messageSubscription_" + subscriptionName);
        }
        execution.SetVariableLocal("_messageSubscriptionActive", false);
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
