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

internal static class JobRetryExecution
{
    public static async Task ExecuteAsync(
        ICommandContext context,
        string jobId,
        string? exceptionMessage,
        CancellationToken cancellationToken)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            throw new WorkflowEngineException("Process engine job manager is not configured.");

        var job = await jobManager.GetJobAsync(jobId, cancellationToken);
        if (job == null)
            throw new WorkflowEngineObjectNotFoundException($"No job found with id '{jobId}'", typeof(JobEntity));

        var retries = Math.Max(0, job.Retries - 1);
        await jobManager.SetJobRetriesAsync(jobId, retries, cancellationToken);
        if (retries == 0)
        {
            if (jobManager is not IJobLifecycleManager lifecycleManager)
                throw new WorkflowEngineException("Current job manager does not support lifecycle operations for moving failed jobs to dead letter.");

            await lifecycleManager.MoveJobToDeadLetterAsync(jobId, exceptionMessage, cancellationToken);
        }
    }
}

