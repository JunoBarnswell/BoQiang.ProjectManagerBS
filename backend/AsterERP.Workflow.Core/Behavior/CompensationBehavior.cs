using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Helper;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class CompensationEventActivityBehavior : FlowNodeActivityBehavior
{
    protected BpmnModelNs.CompensateEventDefinition? CompensateEventDefinition { get; set; }

    public CompensationEventActivityBehavior() { }

    public CompensationEventActivityBehavior(BpmnModelNs.CompensateEventDefinition compensateEventDefinition)
    {
        CompensateEventDefinition = compensateEventDefinition;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariable("_compensationEvent", true);
        if (CompensateEventDefinition != null && !string.IsNullOrEmpty(CompensateEventDefinition.ActivityRef))
        {
            execution.SetVariable("_compensationActivityRef", CompensateEventDefinition.ActivityRef);
        }

        var compensateSubscriptions = FindCompensateEventSubscriptions(execution);
        if (compensateSubscriptions.Count > 0)
        {
            ScopeUtil.ThrowCompensationEvent(compensateSubscriptions, execution, true);
        }

        await LeaveAsync(execution, cancellationToken);
    }

    protected List<CompensateEventSubscription> FindCompensateEventSubscriptions(ExecutionEntity execution)
    {
        var subscriptions = new List<CompensateEventSubscription>();
        var activityRef = CompensateEventDefinition?.ActivityRef;

        var root = FindRootExecution(execution);
        FindCompensateSubscriptionsInTree(root, activityRef, subscriptions);

        return subscriptions;
    }

    protected void FindCompensateSubscriptionsInTree(ExecutionEntity execution, string? activityRef, List<CompensateEventSubscription> subscriptions)
    {
        var hasCompensation = execution.GetVariable("_compensationSubscription") as bool?;
        if (hasCompensation == true)
        {
            var compensationActivityRef = execution.GetVariable("_compensationActivityRef") as string;

            if (string.IsNullOrEmpty(activityRef) || compensationActivityRef == activityRef)
            {
                subscriptions.Add(new CompensateEventSubscription
                {
                    Id = execution.Id,
                    ExecutionId = execution.Id,
                    ActivityId = execution.CurrentActivityId,
                    EventType = "compensate",
                    ProcessInstanceId = execution.ProcessInstanceId
                });
            }
        }

        foreach (var child in execution.ChildExecutions)
        {
            FindCompensateSubscriptionsInTree(child, activityRef, subscriptions);
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
}

public class CompensateBoundaryEventActivityBehavior : BoundaryEventActivityBehavior
{
    protected BpmnModelNs.CompensateEventDefinition? CompensateEventDefinition { get; set; }

    public CompensateBoundaryEventActivityBehavior() { }

    public CompensateBoundaryEventActivityBehavior(
        BpmnModelNs.CompensateEventDefinition compensateEventDefinition,
        bool interrupting)
        : base(interrupting)
    {
        CompensateEventDefinition = compensateEventDefinition;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariable("_compensationSubscription", true);
        execution.SetVariableLocal("_compensationBoundaryActive", true);
        if (CompensateEventDefinition != null && !string.IsNullOrEmpty(CompensateEventDefinition.ActivityRef))
        {
            execution.SetVariable("_compensationActivityRef", CompensateEventDefinition.ActivityRef);
            execution.SetVariableLocal("_compensationBoundaryActivityRef", CompensateEventDefinition.ActivityRef);
        }
    }

    public override async Task TriggerAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_compensationBoundaryActive", false);

        if (CancelActivity)
        {
            execution.SetVariable("_compensationSubscription", false);
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
