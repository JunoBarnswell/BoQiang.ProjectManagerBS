using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class ExecuteAsyncJobCmd : ICommand<object?>
{
    private readonly JobEntity _job;

    public ExecuteAsyncJobCmd(JobEntity job)
    {
        _job = job ?? throw new ArgumentNullException(nameof(job));
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_job.Id))
            throw new WorkflowEngineArgumentException("jobId is null");

        var execution = await ResolveExecutionAsync(context, _job.ExecutionId, cancellationToken);
        var handler = JobHandlerRegistry.Resolve(_job.HandlerType);
        if (handler == null)
            throw new WorkflowEngineException($"No job handler registered for handler type '{_job.HandlerType ?? "<null>"}'.");

        await handler.ExecuteAsync(
            _job,
            _job.HandlerConfiguration ?? string.Empty,
            execution,
            context,
            cancellationToken);

        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            throw new WorkflowEngineException("Process engine job manager is not configured.");

        await jobManager.ExecuteJobAsync(_job.Id!, cancellationToken);

        JobCommandEvents.DispatchEntityEvent(context, WorkflowEventType.JOB_EXECUTION_SUCCESS, new { JobId = _job.Id });
        return null;
    }

    private static async Task<AsterERP.Workflow.Core.Execution.ExecutionEntity?> ResolveExecutionAsync(
        ICommandContext context,
        string? executionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executionId))
            return null;

        try
        {
            return await context.GetCurrentExecutionAsync(executionId, cancellationToken);
        }
        catch
        {
            throw new WorkflowEngineObjectNotFoundException($"No execution found with id '{executionId}'", typeof(AsterERP.Workflow.Core.Execution.ExecutionEntity));
        }
    }
}

