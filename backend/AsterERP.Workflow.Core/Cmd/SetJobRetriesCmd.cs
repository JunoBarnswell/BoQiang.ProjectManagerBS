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

public class SetJobRetriesCmd : ICommand<object?>
{
    private readonly string _jobId;
    private readonly int _retries;

    public SetJobRetriesCmd(string jobId, int retries)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new WorkflowEngineArgumentException(
                $"The job id is mandatory, but '{jobId}' has been provided.");
        if (retries < 0)
            throw new WorkflowEngineArgumentException(
                $"The number of job retries must be a non-negative Integer, but '{retries}' has been provided.");

        _jobId = jobId;
        _retries = retries;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            throw new WorkflowEngineException("Process engine job manager is not configured.");

        var job = await jobManager.GetJobAsync(_jobId, cancellationToken);
        if (job == null)
            throw new WorkflowEngineObjectNotFoundException($"No job found with id '{_jobId}'", typeof(JobEntity));
        if (job.JobType is AbstractJobEntity.JobTypeTimer or AbstractJobEntity.JobTypeDeadLetter)
            throw new WorkflowEngineObjectNotFoundException(
                $"No executable/message job found with id '{_jobId}', actual type is '{job.JobType}'.",
                typeof(JobEntity));

        await jobManager.SetJobRetriesAsync(_jobId, _retries, cancellationToken);

        JobCommandEvents.DispatchEntityEvent(context, WorkflowEventType.ENTITY_UPDATED, new { JobId = _jobId, Retries = _retries });
        return null;
    }
}

