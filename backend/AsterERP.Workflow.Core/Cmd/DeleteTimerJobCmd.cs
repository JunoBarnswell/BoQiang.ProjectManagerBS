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

public class DeleteTimerJobCmd : ICommand<object?>
{
    private readonly string _timerJobId;

    public DeleteTimerJobCmd(string timerJobId)
    {
        if (string.IsNullOrWhiteSpace(timerJobId))
            throw new WorkflowEngineArgumentException("jobId is null");

        _timerJobId = timerJobId;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            throw new WorkflowEngineException("Process engine job manager is not configured.");

        var job = await jobManager.GetJobAsync(_timerJobId, cancellationToken);
        if (job == null)
            throw new WorkflowEngineObjectNotFoundException($"No timer job found with id '{_timerJobId}'", typeof(JobEntity));
        if (job.JobType != AbstractJobEntity.JobTypeTimer)
            throw new WorkflowEngineObjectNotFoundException(
                $"No timer job found with id '{_timerJobId}', actual type is '{job.JobType}'.",
                typeof(JobEntity));

        await jobManager.DeleteJobAsync(_timerJobId, cancellationToken);

        JobCommandEvents.DispatchEntityEvent(context, WorkflowEventType.JOB_CANCELED, new { JobId = _timerJobId, JobType = "timer" });
        return null;
    }
}

