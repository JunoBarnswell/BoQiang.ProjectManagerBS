using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Job;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class IntermediateCatchEventActivityBehavior : FlowNodeActivityBehavior, ITriggerableActivityBehavior
{
    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.IsActive = false;
        execution.SetVariableLocal("_intermediateCatchWaiting", true);
    }

    public virtual async Task TriggerAsync(ExecutionEntity execution, string? signalName, object? signalData, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_intermediateCatchWaiting", false);
        await LeaveIntermediateCatchEvent(execution, cancellationToken);
    }

    protected virtual async Task LeaveIntermediateCatchEvent(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var eventGateway = GetPrecedingEventBasedGateway(execution);
        if (eventGateway != null)
        {
            DeleteOtherEventsRelatedToEventBasedGateway(execution, eventGateway);
        }

        await LeaveAsync(execution, cancellationToken);
    }

    public virtual Task EventCancelledByEventGatewayAsync(
        ExecutionEntity execution,
        CancellationToken cancellationToken = default)
    {
        execution.IsEnded = true;
        execution.IsActive = false;
        return Task.CompletedTask;
    }

    protected BpmnModelNs.EventGateway? GetPrecedingEventBasedGateway(ExecutionEntity execution)
    {
        if (execution.CurrentFlowElement is BpmnModelNs.IntermediateCatchEvent intermediateCatchEvent)
        {
            var incomingFlows = intermediateCatchEvent.IncomingFlows;
            if (incomingFlows != null && incomingFlows.Count == 1)
            {
                var sourceElement = incomingFlows[0].SourceFlowElement;
                if (sourceElement is BpmnModelNs.EventGateway eventGateway)
                {
                    return eventGateway;
                }
            }
        }
        return null;
    }

    protected void DeleteOtherEventsRelatedToEventBasedGateway(ExecutionEntity execution, BpmnModelNs.EventGateway eventGateway)
    {
        var outgoingFlows = eventGateway.OutgoingFlows;
        if (outgoingFlows == null) return;

        var eventActivityIds = new HashSet<string>();
        foreach (var outgoingFlow in outgoingFlows)
        {
            if (outgoingFlow.TargetFlowElement != null &&
                outgoingFlow.TargetFlowElement.Id != execution.CurrentActivityId)
            {
                eventActivityIds.Add(outgoingFlow.TargetFlowElement.Id);
            }
        }

        if (execution.Parent != null)
        {
            foreach (var childExecution in execution.Parent.ChildExecutions.ToList())
            {
                if (eventActivityIds.Contains(childExecution.CurrentActivityId ?? "") &&
                    !childExecution.IsEnded)
                {
                    childExecution.IsEnded = true;
                    childExecution.IsActive = false;
                    eventActivityIds.Remove(childExecution.CurrentActivityId ?? "");
                }
            }
        }
    }
}

public class IntermediateCatchTimerEventActivityBehavior : IntermediateCatchEventActivityBehavior
{
    protected BpmnModelNs.TimerEventDefinition? TimerEventDefinition { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IJobManager? JobManager { get; set; }

    public IntermediateCatchTimerEventActivityBehavior() { }

    public IntermediateCatchTimerEventActivityBehavior(
        BpmnModelNs.TimerEventDefinition timerEventDefinition,
        IExpressionManager? expressionManager = null,
        IJobManager? jobManager = null)
    {
        TimerEventDefinition = timerEventDefinition;
        ExpressionManager = expressionManager;
        JobManager = jobManager;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (TimerEventDefinition != null && JobManager != null)
        {
            var dueDate = ResolveDueDate(execution);
            var handlerConfiguration = execution.CurrentActivityId;
            if (TimerEventDefinition.CalendarName != null)
            {
                handlerConfiguration += "|calendar:" + TimerEventDefinition.CalendarName;
            }

            var timerJob = await JobManager.CreateTimerJobAsync(
                execution.Id,
                execution.ProcessInstanceId!,
                execution.ProcessDefinitionId!,
                dueDate,
                TimerEventDefinition.TimeCycle,
                "timer-intermediate-catch",
                handlerConfiguration,
                execution.TenantId,
                cancellationToken);

            if (timerJob != null)
            {
                await JobManager.ScheduleTimerJobAsync(timerJob, cancellationToken);
            }
        }

        execution.IsActive = false;
        execution.SetVariableLocal("_intermediateCatchWaiting", true);
    }

    public override async Task EventCancelledByEventGatewayAsync(
        ExecutionEntity execution,
        CancellationToken cancellationToken = default)
    {
        if (JobManager != null && execution.CurrentActivityId != null)
        {
            await JobManager.CancelTimerJobAsync(
                execution.Id,
                execution.CurrentActivityId,
                cancellationToken);
        }

        execution.IsEnded = true;
        execution.IsActive = false;
    }

    protected DateTime? ResolveDueDate(ExecutionEntity execution)
    {
        if (TimerEventDefinition == null) return null;

        if (!string.IsNullOrEmpty(TimerEventDefinition.TimeDate))
        {
            if (ExpressionManager != null)
            {
                var result = ExpressionManager.Evaluate(TimerEventDefinition.TimeDate, execution.Variables);
                if (result is DateTime dt) return dt;
                if (result is string s && DateTime.TryParse(s, out var parsed)) return parsed;
            }
            if (DateTime.TryParse(TimerEventDefinition.TimeDate, out var date)) return date;
        }

        if (!string.IsNullOrEmpty(TimerEventDefinition.TimeDuration))
        {
            var timeDurationDueDate = TimerEventActivityBehavior.ResolveTimeDurationDueDate(
                TimerEventDefinition.TimeDuration,
                ExpressionManager,
                execution.Variables);
            if (timeDurationDueDate.HasValue)
            {
                return timeDurationDueDate;
            }
        }

        return null;
    }
}
