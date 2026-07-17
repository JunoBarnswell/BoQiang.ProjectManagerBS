using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class CompleteTaskWithTransientCmd : NeedsActiveTaskCmd<object?>
{
    private readonly Dictionary<string, object?>? _variables;
    private readonly Dictionary<string, object?>? _transientVariables;

    public CompleteTaskWithTransientCmd(string taskId, Dictionary<string, object?>? variables, Dictionary<string, object?>? transientVariables)
        : base(taskId)
    {
        _variables = variables;
        _transientVariables = transientVariables;
    }

    protected override object? Execute(ICommandContext context, TaskImplementation task)
    {
        if (task.DelegationState == "PENDING")
            throw new WorkflowEngineException("A delegated task cannot be completed, but should be resolved instead.");

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateTaskCompletedEvent(task.Id, task.ProcessInstanceId ?? ""));
        }

        return null;
    }

    protected override string GetSuspendedTaskException() => "Cannot complete a suspended task";
}

public class ResolveTaskWithTransientCmd : NeedsActiveTaskCmd<object?>
{
    private readonly Dictionary<string, object?>? _variables;
    private readonly Dictionary<string, object?>? _transientVariables;

    public ResolveTaskWithTransientCmd(string taskId, Dictionary<string, object?>? variables, Dictionary<string, object?>? transientVariables)
        : base(taskId)
    {
        _variables = variables;
        _transientVariables = transientVariables;
    }

    protected override object? Execute(ICommandContext context, TaskImplementation task)
    {
        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.TASK_COMPLETED,
                    new { TaskId = TaskId }));
        }

        return null;
    }

    protected override string GetSuspendedTaskException() => "Cannot resolve a suspended task";
}

public class TriggerWithTransientCmd : NeedsActiveExecutionCmd<object?>
{
    private readonly Dictionary<string, object?>? _processVariables;
    private readonly Dictionary<string, object?>? _transientVariables;

    public TriggerWithTransientCmd(string executionId, Dictionary<string, object?>? processVariables, Dictionary<string, object?>? transientVariables)
        : base(executionId)
    {
        _processVariables = processVariables;
        _transientVariables = transientVariables;
    }

    protected override object? Execute(ICommandContext context, Execution.ExecutionEntity execution)
    {
        if (_processVariables != null)
        {
            foreach (var kvp in _processVariables)
                execution.SetVariable(kvp.Key, kvp.Value);
        }

        return null;
    }

    protected override string GetSuspendedExceptionMessage() => "Cannot trigger a suspended execution";
}

public class CreateDeploymentCmd : ICommand<string>
{
    public string Execute(ICommandContext context)
    {
        return $"deployment-{AbpTimeIdProvider.NewGuid("N")}";
    }

    public Task<string> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

public class GetJobExceptionStacktraceCmd : ICommand<string?>
{
    private readonly string _jobId;
    private readonly string _jobType;

    public GetJobExceptionStacktraceCmd(string jobId, string jobType)
    {
        _jobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
        _jobType = jobType;
    }

    public string? Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_jobId))
            throw new WorkflowEngineArgumentException("jobId is null");
        return null;
    }

    public Task<string?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

