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

public class SignalEventCatchBehavior : FlowNodeActivityBehavior, ITriggerableActivityBehavior
{
    protected BpmnModelNs.SignalEventDefinition? SignalEventDefinition { get; set; }
    protected BpmnModelNs.Signal? Signal { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }

    public SignalEventCatchBehavior() { }

    public SignalEventCatchBehavior(
        BpmnModelNs.SignalEventDefinition signalEventDefinition,
        BpmnModelNs.Signal? signal = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
    {
        SignalEventDefinition = signalEventDefinition;
        Signal = signal;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var signalName = ResolveSignalName(execution);
        if (signalName != null)
        {
            execution.SetVariable("_signalSubscription_" + signalName, true);
            execution.SetVariableLocal("_signalSubscriptionName", signalName);
            execution.SetVariableLocal("_signalSubscriptionActive", true);
            execution.IsActive = false;

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

    public virtual async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, string? signalName = null, object? signalData = null, CancellationToken cancellationToken = default)
    {
        DeleteSignalEventSubscription(execution);

        if (signalData != null)
        {
            execution.SetVariable("_signalData", signalData);
        }

        if (!string.IsNullOrEmpty(signalName))
        {
            execution.SetVariable("_caughtSignalName", signalName);
        }

        execution.IsActive = true;
        await LeaveAsync(execution, cancellationToken);
    }

    protected void DeleteSignalEventSubscription(ExecutionEntity execution)
    {
        var subscriptionName = execution.GetVariableLocal("_signalSubscriptionName") as string;
        if (subscriptionName != null)
        {
            execution.RemoveVariable("_signalSubscription_" + subscriptionName);
        }
        execution.SetVariableLocal("_signalSubscriptionActive", false);
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

public class SignalStartEventActivityBehavior : FlowNodeActivityBehavior
{
    protected BpmnModelNs.SignalEventDefinition? SignalEventDefinition { get; set; }
    protected BpmnModelNs.Signal? Signal { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }

    public SignalStartEventActivityBehavior() { }

    public SignalStartEventActivityBehavior(
        BpmnModelNs.SignalEventDefinition signalEventDefinition,
        BpmnModelNs.Signal? signal = null,
        IExpressionManager? expressionManager = null)
    {
        SignalEventDefinition = signalEventDefinition;
        Signal = signal;
        ExpressionManager = expressionManager;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var signalName = ResolveSignalName(execution);
        if (signalName != null)
        {
            execution.SetVariable("_signalSubscription_" + signalName, true);
        }
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
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

public class SignalBoundaryEventActivityBehavior : BoundaryEventActivityBehavior, ITriggerableActivityBehavior
{
    protected BpmnModelNs.SignalEventDefinition? SignalEventDefinition { get; set; }
    protected BpmnModelNs.Signal? Signal { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }

    public SignalBoundaryEventActivityBehavior() { }

    public SignalBoundaryEventActivityBehavior(
        BpmnModelNs.SignalEventDefinition signalEventDefinition,
        BpmnModelNs.Signal? signal,
        bool interrupting,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
        : base(interrupting)
    {
        SignalEventDefinition = signalEventDefinition;
        Signal = signal;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var signalName = ResolveSignalName(execution);
        if (signalName != null)
        {
            execution.SetVariable("_signalSubscription_" + signalName, true);
            execution.SetVariableLocal("_signalSubscriptionName", signalName);
            execution.SetVariableLocal("_signalSubscriptionActive", true);

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

    public virtual async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, string? signalName = null, object? signalData = null, CancellationToken cancellationToken = default)
    {
        if (CancelActivity)
        {
            DeleteSignalEventSubscription(execution);
        }

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

    protected void DeleteSignalEventSubscription(ExecutionEntity execution)
    {
        var subscriptionName = execution.GetVariableLocal("_signalSubscriptionName") as string;
        if (subscriptionName != null)
        {
            execution.RemoveVariable("_signalSubscription_" + subscriptionName);
        }
        execution.SetVariableLocal("_signalSubscriptionActive", false);
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

public class SignalThrowEventActivityBehavior : FlowNodeActivityBehavior
{
    protected BpmnModelNs.SignalEventDefinition? SignalEventDefinition { get; set; }
    protected BpmnModelNs.Signal? Signal { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }
    protected bool ProcessInstanceScope { get; set; }

    public SignalThrowEventActivityBehavior() { }

    public SignalThrowEventActivityBehavior(
        BpmnModelNs.SignalEventDefinition signalEventDefinition,
        BpmnModelNs.Signal? signal = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
    {
        SignalEventDefinition = signalEventDefinition;
        Signal = signal;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;

        if (signal != null)
        {
            ProcessInstanceScope = signal.Scope == "processInstance";
        }
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var signalName = ResolveSignalName(execution);
        if (signalName != null)
        {
            execution.SetVariable("_signalThrown_" + signalName, true);
            execution.EventName = signalName;

            var waitingExecutions = FindWaitingExecutions(execution, signalName);
            foreach (var waitingExecution in waitingExecutions)
            {
                var signalVariables = execution.Variables != null && execution.Variables.Count > 0
                    ? new Dictionary<string, object?>(execution.Variables)
                    : null;

                if (signalVariables != null)
                {
                    foreach (var kv in signalVariables)
                    {
                        waitingExecution.SetVariable(kv.Key, kv.Value);
                    }
                }

                waitingExecution.SetVariableLocal("_signalSubscriptionActive", false);
                waitingExecution.SetVariable("_caughtSignalName", signalName);
            }

            if (EventDispatcher != null && EventDispatcher.IsEnabled)
            {
                EventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateCustomEvent(
                    "signalReceived",
                    new Dictionary<string, object?>
                    {
                        { "signalName", signalName },
                        { "activityId", execution.CurrentActivityId },
                        { "processInstanceId", execution.ProcessInstanceId }
                    }));
            }
        }

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    protected List<ExecutionEntity> FindWaitingExecutions(ExecutionEntity sourceExecution, string signalName)
    {
        var results = new List<ExecutionEntity>();
        var rootExecution = FindRootExecution(sourceExecution);

        if (ProcessInstanceScope)
        {
            FindWaitingExecutionsInTree(rootExecution, signalName, results);
        }
        else
        {
            FindWaitingExecutionsInTree(rootExecution, signalName, results);
        }

        return results;
    }

    protected void FindWaitingExecutionsInTree(ExecutionEntity execution, string signalName, List<ExecutionEntity> results)
    {
        var subscriptionActive = execution.GetVariableLocal("_signalSubscriptionActive") as bool?;
        var subscriptionName = execution.GetVariableLocal("_signalSubscriptionName") as string;

        if (subscriptionActive == true && subscriptionName == signalName && execution.Id != execution.Id)
        {
            results.Add(execution);
        }

        foreach (var child in execution.ChildExecutions)
        {
            FindWaitingExecutionsInTree(child, signalName, results);
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

public class IntermediateSignalCatchEventActivityBehavior : FlowNodeActivityBehavior, ITriggerableActivityBehavior
{
    protected BpmnModelNs.SignalEventDefinition? SignalEventDefinition { get; set; }
    protected BpmnModelNs.Signal? Signal { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IEventDispatcher? EventDispatcher { get; set; }

    public IntermediateSignalCatchEventActivityBehavior() { }

    public IntermediateSignalCatchEventActivityBehavior(
        BpmnModelNs.SignalEventDefinition signalEventDefinition,
        BpmnModelNs.Signal? signal = null,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null)
    {
        SignalEventDefinition = signalEventDefinition;
        Signal = signal;
        ExpressionManager = expressionManager;
        EventDispatcher = eventDispatcher;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var signalName = ResolveSignalName(execution);
        if (signalName != null)
        {
            execution.SetVariable("_signalSubscription_" + signalName, true);
            execution.SetVariableLocal("_signalSubscriptionName", signalName);
            execution.SetVariableLocal("_signalSubscriptionActive", true);
            execution.IsActive = false;

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

    public virtual async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, string? signalName = null, object? signalData = null, CancellationToken cancellationToken = default)
    {
        DeleteSignalEventSubscription(execution);

        if (signalData != null)
        {
            execution.SetVariable("_signalData", signalData);
        }

        if (!string.IsNullOrEmpty(signalName))
        {
            execution.SetVariable("_caughtSignalName", signalName);
        }

        execution.IsActive = true;
        await LeaveAsync(execution, cancellationToken);
    }

    protected void DeleteSignalEventSubscription(ExecutionEntity execution)
    {
        var subscriptionName = execution.GetVariableLocal("_signalSubscriptionName") as string;
        if (subscriptionName != null)
        {
            execution.RemoveVariable("_signalSubscription_" + subscriptionName);
        }
        execution.SetVariableLocal("_signalSubscriptionActive", false);
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
