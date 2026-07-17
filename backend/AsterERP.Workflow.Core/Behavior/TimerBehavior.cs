using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Job;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class TimerEventActivityBehavior : FlowNodeActivityBehavior
{
    protected BpmnModelNs.TimerEventDefinition? TimerEventDefinition { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IJobManager? JobManager { get; set; }

    public TimerEventActivityBehavior() { }

    public TimerEventActivityBehavior(
        BpmnModelNs.TimerEventDefinition timerEventDefinition,
        IExpressionManager? expressionManager = null,
        IJobManager? jobManager = null)
    {
        TimerEventDefinition = timerEventDefinition;
        ExpressionManager = expressionManager;
        JobManager = jobManager;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (TimerEventDefinition != null && JobManager != null)
        {
            var dueDate = ResolveDueDate(execution);
            var handlerType = "timer";
            var handlerConfiguration = ResolveHandlerConfiguration(execution);

            var timerJob = await JobManager.CreateTimerJobAsync(
                execution.Id,
                execution.ProcessInstanceId!,
                execution.ProcessDefinitionId!,
                dueDate,
                TimerEventDefinition.TimeCycle,
                handlerType,
                handlerConfiguration,
                execution.TenantId,
                cancellationToken);

            if (timerJob != null)
            {
                await JobManager.ScheduleTimerJobAsync(timerJob, cancellationToken);
            }
        }
        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual string ResolveHandlerConfiguration(ExecutionEntity execution)
    {
        var config = execution.CurrentActivityId;
        if (TimerEventDefinition?.EndDate != null)
        {
            string? endDateStr = null;
            if (DateTime.TryParse(TimerEventDefinition.EndDate, out var directDate))
            {
                endDateStr = directDate.ToString("o");
            }
            else if (ExpressionManager != null)
            {
                try
                {
                    var endDate = ExpressionManager.Evaluate(TimerEventDefinition.EndDate, execution.Variables);
                    if (endDate != null)
                    {
                        endDateStr = endDate is DateTime dt ? dt.ToString("o") : endDate.ToString();
                    }
                }
                catch
                {
                    endDateStr = TimerEventDefinition.EndDate;
                }
            }
            if (endDateStr != null)
            {
                config += "|endDate:" + endDateStr;
            }
        }
        if (TimerEventDefinition?.CalendarName != null)
        {
            config += "|calendar:" + TimerEventDefinition.CalendarName;
        }
        return config;
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
            var timeDurationDueDate = ResolveTimeDurationDueDate(
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

    public static bool TryParseDuration(string duration, out TimeSpan timeSpan)
    {
        timeSpan = TimeSpan.Zero;
        if (string.IsNullOrEmpty(duration)) return false;

        if (duration.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
        {
            var inner = duration.Substring(2);
            if (inner.EndsWith('H') && double.TryParse(inner.TrimEnd('H'), out var hours))
            {
                timeSpan = TimeSpan.FromHours(hours);
                return true;
            }
            if (inner.EndsWith('M') && double.TryParse(inner.TrimEnd('M'), out var minutes))
            {
                timeSpan = TimeSpan.FromMinutes(minutes);
                return true;
            }
            if (inner.EndsWith('S') && double.TryParse(inner.TrimEnd('S'), out var seconds))
            {
                timeSpan = TimeSpan.FromSeconds(seconds);
                return true;
            }
        }

        return TimeSpan.TryParse(duration, out timeSpan);
    }

    public static DateTime? ResolveTimeDurationDueDate(
        string? timeDuration,
        IExpressionManager? expressionManager,
        Dictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(timeDuration))
        {
            return null;
        }

        if (TryParseDuration(timeDuration, out var literalDuration))
        {
            return AbpTimeIdProvider.UtcNow.Add(literalDuration);
        }

        if (expressionManager != null)
        {
            var result = expressionManager.Evaluate(timeDuration, variables);
            if (result is TimeSpan timeSpan)
            {
                return AbpTimeIdProvider.UtcNow.Add(timeSpan);
            }

            if (result is string durationText && TryParseDuration(durationText, out var evaluatedDuration))
            {
                return AbpTimeIdProvider.UtcNow.Add(evaluatedDuration);
            }
        }

        return TryParseDuration(timeDuration, out var fallbackDuration)
            ? AbpTimeIdProvider.UtcNow.Add(fallbackDuration)
            : null;
    }
}

public class TimerCatchEventActivityBehavior : FlowNodeActivityBehavior
{
    protected BpmnModelNs.TimerEventDefinition? TimerEventDefinition { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IJobManager? JobManager { get; set; }
    public string? AttachedToActivityId { get; set; }

    public TimerCatchEventActivityBehavior() { }

    public TimerCatchEventActivityBehavior(
        BpmnModelNs.TimerEventDefinition timerEventDefinition,
        IExpressionManager? expressionManager = null,
        IJobManager? jobManager = null)
    {
        TimerEventDefinition = timerEventDefinition;
        ExpressionManager = expressionManager;
        JobManager = jobManager;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (TimerEventDefinition != null && JobManager != null)
        {
            var dueDate = ResolveDueDate(execution);
            var handlerType = "timer-catch";
            var handlerConfiguration = ResolveHandlerConfiguration(execution);

            var timerJob = await JobManager.CreateTimerJobAsync(
                execution.Id,
                execution.ProcessInstanceId!,
                execution.ProcessDefinitionId!,
                dueDate,
                TimerEventDefinition.TimeCycle,
                handlerType,
                handlerConfiguration,
                execution.TenantId,
                cancellationToken);

            if (timerJob != null)
            {
                await JobManager.ScheduleTimerJobAsync(timerJob, cancellationToken);
            }
        }
    }

    public virtual async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }

    protected string ResolveHandlerConfiguration(ExecutionEntity execution)
    {
        var config = execution.CurrentActivityId;
        if (TimerEventDefinition?.EndDate != null)
        {
            string? endDateStr = null;
            if (DateTime.TryParse(TimerEventDefinition.EndDate, out var directDate))
            {
                endDateStr = directDate.ToString("o");
            }
            else if (ExpressionManager != null)
            {
                try
                {
                    var endDate = ExpressionManager.Evaluate(TimerEventDefinition.EndDate, execution.Variables);
                    if (endDate != null)
                    {
                        endDateStr = endDate is DateTime dt ? dt.ToString("o") : endDate.ToString();
                    }
                }
                catch
                {
                    endDateStr = TimerEventDefinition.EndDate;
                }
            }
            if (endDateStr != null)
            {
                config += "|endDate:" + endDateStr;
            }
        }
        if (TimerEventDefinition?.CalendarName != null)
        {
            config += "|calendar:" + TimerEventDefinition.CalendarName;
        }
        if (AttachedToActivityId != null)
        {
            config += "|attachedTo:" + AttachedToActivityId;
        }
        return config;
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

public class TimerStartEventActivityBehavior : FlowNodeActivityBehavior
{
    protected BpmnModelNs.TimerEventDefinition? TimerEventDefinition { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IJobManager? JobManager { get; set; }
    public string? ProcessDefinitionId { get; set; }
    public bool IsInterrupting { get; set; } = true;

    public TimerStartEventActivityBehavior() { }

    public TimerStartEventActivityBehavior(
        BpmnModelNs.TimerEventDefinition timerEventDefinition,
        IExpressionManager? expressionManager = null,
        IJobManager? jobManager = null)
    {
        TimerEventDefinition = timerEventDefinition;
        ExpressionManager = expressionManager;
        JobManager = jobManager;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (TimerEventDefinition != null && JobManager != null)
        {
            var dueDate = ResolveDueDate(execution);
            var handlerType = "timer-start";
            var handlerConfiguration = ResolveHandlerConfiguration(execution);

            var timerJob = await JobManager.CreateTimerJobAsync(
                execution.Id,
                execution.ProcessInstanceId!,
                execution.ProcessDefinitionId!,
                dueDate,
                TimerEventDefinition.TimeCycle,
                handlerType,
                handlerConfiguration,
                execution.TenantId,
                cancellationToken);

            if (timerJob != null)
            {
                await JobManager.ScheduleTimerJobAsync(timerJob, cancellationToken);
            }

            execution.IsActive = false;
            await LeaveAsync(execution, cancellationToken);
        }
    }

    public virtual async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, string? signalName = null, object? signalData = null, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }

    protected string ResolveHandlerConfiguration(ExecutionEntity execution)
    {
        var config = execution.CurrentActivityId;
        if (ProcessDefinitionId != null)
        {
            config += "|processDefinitionId:" + ProcessDefinitionId;
        }
        if (IsInterrupting)
        {
            config += "|interrupting:true";
        }
        if (TimerEventDefinition?.EndDate != null)
        {
            string? endDateStr = null;
            if (DateTime.TryParse(TimerEventDefinition.EndDate, out var directDate))
            {
                endDateStr = directDate.ToString("o");
            }
            else if (ExpressionManager != null)
            {
                try
                {
                    var endDate = ExpressionManager.Evaluate(TimerEventDefinition.EndDate, execution.Variables);
                    if (endDate != null)
                    {
                        endDateStr = endDate is DateTime dt ? dt.ToString("o") : endDate.ToString();
                    }
                }
                catch
                {
                    endDateStr = TimerEventDefinition.EndDate;
                }
            }
            if (endDateStr != null)
            {
                config += "|endDate:" + endDateStr;
            }
        }
        if (TimerEventDefinition?.CalendarName != null)
        {
            config += "|calendar:" + TimerEventDefinition.CalendarName;
        }
        return config;
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

public class TimerBoundaryEventActivityBehavior : BoundaryEventActivityBehavior
{
    protected BpmnModelNs.TimerEventDefinition? TimerEventDefinition { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }
    protected IJobManager? JobManager { get; set; }
    public string? AttachedToActivityId { get; set; }
    public TimerBoundaryEventActivityBehavior(
        BpmnModelNs.TimerEventDefinition timerEventDefinition,
        bool interrupting,
        IExpressionManager? expressionManager = null,
        IJobManager? jobManager = null)
        : base(interrupting)
    {
        TimerEventDefinition = timerEventDefinition;
        ExpressionManager = expressionManager;
        JobManager = jobManager;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (TimerEventDefinition != null && JobManager != null)
        {
            var dueDate = ResolveDueDate(execution);
            var handlerType = "timer-boundary";
            var handlerConfiguration = ResolveHandlerConfiguration(execution);

            var timerJob = await JobManager.CreateTimerJobAsync(
                execution.Id,
                execution.ProcessInstanceId!,
                execution.ProcessDefinitionId!,
                dueDate,
                TimerEventDefinition.TimeCycle,
                handlerType,
                handlerConfiguration,
                execution.TenantId,
                cancellationToken);

            if (timerJob != null)
            {
                await JobManager.ScheduleTimerJobAsync(timerJob, cancellationToken);
            }
        }
    }

    public override async global::System.Threading.Tasks.Task TriggerAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (Interrupting)
        {
            await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
        }
        else
        {
            await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
        }
    }

    public virtual async global::System.Threading.Tasks.Task CancelTimerAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (JobManager != null)
        {
            await JobManager.CancelTimerJobAsync(execution.Id, execution.CurrentActivityId, cancellationToken);
        }
    }

    protected string ResolveHandlerConfiguration(ExecutionEntity execution)
    {
        var config = execution.CurrentActivityId;
        if (AttachedToActivityId != null)
        {
            config += "|attachedTo:" + AttachedToActivityId;
        }
        if (base.CancelActivity)
        {
            config += "|cancelActivity:true";
        }
        else
        {
            config += "|cancelActivity:false";
        }
        if (TimerEventDefinition?.EndDate != null)
        {
            string? endDateStr = null;
            if (DateTime.TryParse(TimerEventDefinition.EndDate, out var directDate))
            {
                endDateStr = directDate.ToString("o");
            }
            else if (ExpressionManager != null)
            {
                try
                {
                    var endDate = ExpressionManager.Evaluate(TimerEventDefinition.EndDate, execution.Variables);
                    if (endDate != null)
                    {
                        endDateStr = endDate is DateTime dt ? dt.ToString("o") : endDate.ToString();
                    }
                }
                catch
                {
                    endDateStr = TimerEventDefinition.EndDate;
                }
            }
            if (endDateStr != null)
            {
                config += "|endDate:" + endDateStr;
            }
        }
        if (TimerEventDefinition?.CalendarName != null)
        {
            config += "|calendar:" + TimerEventDefinition.CalendarName;
        }
        return config;
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

