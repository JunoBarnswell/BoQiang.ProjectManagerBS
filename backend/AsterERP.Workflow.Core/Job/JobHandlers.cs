using System.Text.Json;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Job;

public interface IJobHandler
{
    string Type { get; }
    Task ExecuteAsync(
        JobEntity job,
        string configuration,
        ExecutionEntity? execution,
        ICommandContext commandContext,
        CancellationToken cancellationToken = default);
}

public class AsyncContinuationJobHandler : IJobHandler
{
    public const string HandlerType = "async-continuation";
    public string Type => HandlerType;

    public async Task ExecuteAsync(JobEntity job, string configuration, ExecutionEntity? execution, ICommandContext commandContext, CancellationToken cancellationToken = default)
    {
        if (execution?.CurrentFlowElement == null)
            return;

        var agenda = new WorkflowEngineAgenda(commandContext.ProcessEngineConfiguration);
        agenda.PlanContinueProcessOperation(execution);
        await ExecuteAgendaAsync(agenda, cancellationToken);
    }

    private static async Task ExecuteAgendaAsync(IAgenda agenda, CancellationToken cancellationToken)
    {
        while (!agenda.IsEmpty)
            await agenda.ExecuteNextAsync(cancellationToken);
    }
}

public class TriggerTimerEventJobHandler : IJobHandler
{
    public const string HandlerType = "trigger-timer";
    public string Type => HandlerType;

    public async Task ExecuteAsync(JobEntity job, string configuration, ExecutionEntity? execution, ICommandContext commandContext, CancellationToken cancellationToken = default)
    {
        if (execution?.CurrentFlowElement != null)
        {
            var agenda = new WorkflowEngineAgenda(commandContext.ProcessEngineConfiguration);
            agenda.PlanTriggerExecutionOperation(execution);
            await ExecuteAgendaAsync(agenda, cancellationToken);
        }

        DispatchCustomJobEvent(commandContext, "TIMER_FIRED", job);
    }

    private static void DispatchCustomJobEvent(ICommandContext commandContext, string eventName, JobEntity job)
    {
        var dispatcher = commandContext.ProcessEngineConfiguration.EventDispatcher;
        if (!dispatcher.IsEnabled)
            return;

        dispatcher.DispatchEvent(WorkflowEventBuilder.CreateCustomEvent(
            eventName,
            new Dictionary<string, object?>
            {
                ["jobId"] = job.Id,
                ["executionId"] = job.ExecutionId,
                ["processInstanceId"] = job.ProcessInstanceId,
                ["processDefinitionId"] = job.ProcessDefinitionId
            }));
    }

    private static async Task ExecuteAgendaAsync(IAgenda agenda, CancellationToken cancellationToken)
    {
        while (!agenda.IsEmpty)
            await agenda.ExecuteNextAsync(cancellationToken);
    }
}

public class TimerStartEventJobHandler : TimerEventHandler, IJobHandler
{
    public const string HandlerType = "timer-start-event";
    public string Type => HandlerType;

    public async Task ExecuteAsync(JobEntity job, string configuration, ExecutionEntity? execution, ICommandContext commandContext, CancellationToken cancellationToken = default)
    {
        var activityId = GetActivityIdFromConfiguration(configuration);

        DispatchTimerFiredEvent(commandContext, job, activityId);

        if (!string.IsNullOrEmpty(job.ProcessDefinitionId))
        {
            await commandContext.ProcessEngineConfiguration.CommandExecutor.ExecuteAsync(
                new StartProcessInstanceCmd(null, job.ProcessDefinitionId, null, new Dictionary<string, object?>(), job.TenantId),
                cancellationToken);
        }
    }

    private static void DispatchTimerFiredEvent(ICommandContext commandContext, JobEntity job, string activityId)
    {
        var dispatcher = commandContext.ProcessEngineConfiguration.EventDispatcher;
        if (!dispatcher.IsEnabled)
            return;

        dispatcher.DispatchEvent(WorkflowEventBuilder.CreateCustomEvent(
            "TIMER_FIRED",
            new Dictionary<string, object?>
            {
                ["jobId"] = job.Id,
                ["activityId"] = activityId,
                ["processDefinitionId"] = job.ProcessDefinitionId,
                ["tenantId"] = job.TenantId
            }));
    }
}

public class TimerEventHandler
{
    public const string PropertyNameTimerActivityId = "activityId";
    public const string PropertyNameEndDateExpression = "timerEndDate";
    public const string PropertyNameCalendarNameExpression = "calendarName";

    public static string CreateConfiguration(string id, string? endDate, string? calendarName)
    {
        var cfg = new Dictionary<string, object?>
        {
            [PropertyNameTimerActivityId] = id
        };

        if (endDate != null)
            cfg[PropertyNameEndDateExpression] = endDate;

        if (calendarName != null)
            cfg[PropertyNameCalendarNameExpression] = calendarName;

        return JsonSerializer.Serialize(cfg);
    }

    public static string SetActivityIdToConfiguration(string jobHandlerConfiguration, string activityId)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<Dictionary<string, object?>>(jobHandlerConfiguration);
            if (cfg != null)
            {
                cfg[PropertyNameTimerActivityId] = activityId;
                return JsonSerializer.Serialize(cfg);
            }
        }
        catch
        {
        }

        return jobHandlerConfiguration;
    }

    public static string GetActivityIdFromConfiguration(string jobHandlerConfiguration)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jobHandlerConfiguration);
            if (cfg != null && cfg.TryGetValue(PropertyNameTimerActivityId, out var element))
            {
                return element.GetString() ?? jobHandlerConfiguration;
            }
        }
        catch
        {
        }

        return jobHandlerConfiguration;
    }

    public static string GetCalendarNameFromConfiguration(string jobHandlerConfiguration)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jobHandlerConfiguration);
            if (cfg != null && cfg.TryGetValue(PropertyNameCalendarNameExpression, out var element))
            {
                return element.GetString() ?? string.Empty;
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    public static string SetEndDateToConfiguration(string jobHandlerConfiguration, string? endDate)
    {
        Dictionary<string, object?> cfg;
        try
        {
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, object?>>(jobHandlerConfiguration);
            cfg = deserialized ?? new Dictionary<string, object?>();
        }
        catch
        {
            cfg = new Dictionary<string, object?>
            {
                [PropertyNameTimerActivityId] = jobHandlerConfiguration
            };
        }

        if (endDate != null)
            cfg[PropertyNameEndDateExpression] = endDate;

        return JsonSerializer.Serialize(cfg);
    }

    public static string? GetEndDateFromConfiguration(string jobHandlerConfiguration)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jobHandlerConfiguration);
            if (cfg != null && cfg.TryGetValue(PropertyNameEndDateExpression, out var element))
            {
                return element.GetString();
            }
        }
        catch
        {
        }

        return null;
    }
}

public abstract class TimerChangeProcessDefinitionSuspensionStateJobHandler : IJobHandler
{
    public abstract string Type { get; }
    public abstract Task ExecuteAsync(JobEntity job, string configuration, ExecutionEntity? execution, ICommandContext commandContext, CancellationToken cancellationToken = default);

    private const string JobHandlerCfgIncludeProcessInstances = "includeProcessInstances";

    public static string CreateJobHandlerConfiguration(bool includeProcessInstances)
    {
        var json = new Dictionary<string, object?>
        {
            [JobHandlerCfgIncludeProcessInstances] = includeProcessInstances
        };
        return JsonSerializer.Serialize(json);
    }

    protected static bool GetIncludeProcessInstances(string jobHandlerConfiguration)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jobHandlerConfiguration);
            if (cfg != null && cfg.TryGetValue(JobHandlerCfgIncludeProcessInstances, out var element))
            {
                return element.GetBoolean();
            }
        }
        catch
        {
        }

        return false;
    }
}

public class TimerSuspendProcessDefinitionHandler : TimerChangeProcessDefinitionSuspensionStateJobHandler
{
    public const string HandlerType = "suspend-processdefinition";
    public override string Type => HandlerType;

    public override async Task ExecuteAsync(JobEntity job, string configuration, ExecutionEntity? execution, ICommandContext commandContext, CancellationToken cancellationToken = default)
    {
        var includeProcessInstances = GetIncludeProcessInstances(configuration);

        var commandExecutor = commandContext.ProcessEngineConfiguration.CommandExecutor;
        if (commandExecutor == null)
            return;

        var cmd = new Cmd.SuspendProcessDefinitionCmd(
            job.ProcessDefinitionId,
            null,
            includeProcessInstances,
            null,
            job.TenantId);
        await commandExecutor.ExecuteAsync(cmd, cancellationToken);
    }
}

public class TimerActivateProcessDefinitionHandler : TimerChangeProcessDefinitionSuspensionStateJobHandler
{
    public const string HandlerType = "activate-processdefinition";
    public override string Type => HandlerType;

    public override async Task ExecuteAsync(JobEntity job, string configuration, ExecutionEntity? execution, ICommandContext commandContext, CancellationToken cancellationToken = default)
    {
        var includeProcessInstances = GetIncludeProcessInstances(configuration);

        var commandExecutor = commandContext.ProcessEngineConfiguration.CommandExecutor;
        if (commandExecutor == null)
            return;

        var cmd = new Cmd.ActivateProcessDefinitionCmd(
            job.ProcessDefinitionId,
            null,
            includeProcessInstances,
            null,
            job.TenantId);
        await commandExecutor.ExecuteAsync(cmd, cancellationToken);
    }
}

public class ProcessEventJobHandler : IJobHandler
{
    public const string HandlerType = "event";
    public string Type => HandlerType;

    public async Task ExecuteAsync(JobEntity job, string configuration, ExecutionEntity? execution, ICommandContext commandContext, CancellationToken cancellationToken = default)
    {
        var eventConfiguration = ProcessEventJobConfiguration.Parse(configuration);
        if (execution == null && !string.IsNullOrEmpty(eventConfiguration.ExecutionId))
        {
            try
            {
                execution = await commandContext.GetCurrentExecutionAsync(eventConfiguration.ExecutionId, cancellationToken);
            }
            catch
            {
                execution = null;
            }
        }

        if (execution == null)
            return;

        foreach (var variable in eventConfiguration.Payload)
            execution.SetVariable(variable.Key, variable.Value);

        if (!string.IsNullOrEmpty(eventConfiguration.EventName))
            execution.SetVariableLocal("_eventName", eventConfiguration.EventName);

        if (!string.IsNullOrEmpty(eventConfiguration.MessageName))
        {
            execution.SetVariableLocal("_messageSubscriptionActive", false);
            execution.SetVariable("_messageData", eventConfiguration.Payload);
        }

        if (!string.IsNullOrEmpty(eventConfiguration.SignalName))
        {
            execution.SetVariableLocal("_signalSubscriptionActive", false);
            execution.SetVariable("_signalData", eventConfiguration.Payload);
        }

        var dispatcher = commandContext.ProcessEngineConfiguration.EventDispatcher;
        if (dispatcher.IsEnabled)
        {
            dispatcher.DispatchEvent(WorkflowEventBuilder.CreateCustomEvent(
                "PROCESS_EVENT_RECEIVED",
                new Dictionary<string, object?>
                {
                    ["jobId"] = job.Id,
                    ["eventName"] = eventConfiguration.EventName,
                    ["messageName"] = eventConfiguration.MessageName,
                    ["signalName"] = eventConfiguration.SignalName,
                    ["executionId"] = execution.Id
                }));
        }

        return;
    }
}


public enum TimerDeclarationType
{
    Date,
    Duration,
    Cycle
}

public interface IFailedJobCommandFactory
{
    ICommand<object?> CreateCommand(string jobId, Exception exception);
}

public class DefaultFailedJobCommandFactory : IFailedJobCommandFactory
{
    public ICommand<object?> CreateCommand(string jobId, Exception exception)
    {
        return new JobRetryCommand(jobId, exception);
    }
}

public class JobRetryCommand : ICommand<object?>
{
    private readonly string _jobId;
    private readonly Exception _exception;

    public JobRetryCommand(string jobId, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new WorkflowEngineArgumentException("jobId is null");

        _jobId = jobId;
        _exception = exception;
    }


    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            return Task.FromResult<object?>(null);

        return HandleRetryAsync(jobManager, context, cancellationToken);
    }

    private async Task<object?> HandleRetryAsync(IJobManager jobManager, ICommandContext context, CancellationToken cancellationToken)
    {
        var job = await jobManager.GetJobAsync(_jobId, cancellationToken);
        if (job == null)
            return null;

        var retries = Math.Max(0, job.Retries - 1);
        var exceptionMessage = _exception?.Message;
        await jobManager.SetJobRetriesAsync(_jobId, retries, cancellationToken);

        if (retries <= 0)
        {
            if (jobManager is IJobLifecycleManager lifecycleManager)
            {
                await lifecycleManager.MoveJobToDeadLetterAsync(_jobId, exceptionMessage, cancellationToken);
            }
            else
            {
                throw new WorkflowEngineException("Current job manager does not support lifecycle operations for moving failed jobs to dead letter.");
            }
        }

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                new WorkflowEventImplementation(
                    WorkflowEventType.JOB_RETRIES_DECREMENTED,
                    job.ExecutionId,
                    job.ProcessInstanceId,
                    job.ProcessDefinitionId));
        }

        return null;
    }
}

public class FailedJobListener
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly JobEntity _job;
    private readonly IFailedJobCommandFactory _failedJobCommandFactory;
    private readonly IEventDispatcher? _eventDispatcher;

    public FailedJobListener(
        ICommandExecutor commandExecutor,
        JobEntity job,
        IFailedJobCommandFactory failedJobCommandFactory,
        IEventDispatcher? eventDispatcher = null)
    {
        _commandExecutor = commandExecutor;
        _job = job;
        _failedJobCommandFactory = failedJobCommandFactory;
        _eventDispatcher = eventDispatcher;
    }

    public Task OnSuccessAsync()
    {
        _eventDispatcher?.DispatchEvent(
            new WorkflowEventImplementation(WorkflowEventType.JOB_EXECUTION_SUCCESS, _job.ExecutionId, _job.ProcessInstanceId, _job.ProcessDefinitionId));
        return Task.CompletedTask;
    }

    public async Task OnFailureAsync(Exception exception, CancellationToken cancellationToken = default)
    {
        _eventDispatcher?.DispatchEvent(
            new WorkflowEventImplementation(WorkflowEventType.JOB_EXECUTION_FAILURE, _job.ExecutionId, _job.ProcessInstanceId, _job.ProcessDefinitionId));

        var cmd = _failedJobCommandFactory.CreateCommand(_job.Id, exception);
        await _commandExecutor.ExecuteAsync(cmd, cancellationToken);
    }
}

public class AsyncJobAddedNotification
{
    private readonly JobEntity _job;
    private readonly IAsyncJobExecutor _asyncExecutor;

    public AsyncJobAddedNotification(JobEntity job, IAsyncJobExecutor asyncExecutor)
    {
        _job = job;
        _asyncExecutor = asyncExecutor;
    }

    public async Task NotifyAsync()
    {
        await _asyncExecutor.ExecuteJobAsync(_job.Id);
    }
}

public class AcquiredJobs
{
    private readonly List<List<string>> _acquiredJobBatches = new();
    private readonly HashSet<string> _acquiredJobs = new();

    public List<List<string>> JobIdBatches => _acquiredJobBatches;

    public void AddJobIdBatch(List<string> jobIds)
    {
        _acquiredJobBatches.Add(jobIds);
        _acquiredJobs.UnionWith(jobIds);
    }

    public bool Contains(string jobId)
    {
        return _acquiredJobs.Contains(jobId);
    }

    public int Size => _acquiredJobs.Count;
}
