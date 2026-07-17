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

public class DeleteDeadLetterJobCmd : ICommand<object?>
{
    private readonly string _jobId;

    public DeleteDeadLetterJobCmd(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new WorkflowEngineArgumentException("jobId is null");

        _jobId = jobId;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            throw new WorkflowEngineException("Process engine job manager is not configured.");

        var job = await jobManager.GetJobAsync(_jobId, cancellationToken);
        if (job == null)
            throw new WorkflowEngineObjectNotFoundException($"No deadletter job found with id '{_jobId}'", typeof(JobEntity));
        if (job.JobType != AbstractJobEntity.JobTypeDeadLetter)
            throw new WorkflowEngineObjectNotFoundException(
                $"No deadletter job found with id '{_jobId}', actual type is '{job.JobType}'.",
                typeof(JobEntity));

        await jobManager.DeleteJobAsync(_jobId, cancellationToken);

        JobCommandEvents.DispatchEntityEvent(context, WorkflowEventType.JOB_CANCELED, new { JobId = _jobId, JobType = "deadletter" });
        return null;
    }
}

