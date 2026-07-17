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

public class CancelJobsCmd : ICommand<object?>
{
    private readonly string _executionId;

    public CancelJobsCmd(string executionId)
    {
        if (string.IsNullOrWhiteSpace(executionId))
            throw new WorkflowEngineArgumentException("executionId is null");

        _executionId = executionId;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager is not IJobLifecycleManager lifecycleManager)
            throw new WorkflowEngineException("Current job manager does not support lifecycle operations for canceling jobs.");

        var executableJobs = await lifecycleManager.GetJobsAsync(cancellationToken);
        var timerJobs = await lifecycleManager.GetTimerJobsAsync(cancellationToken);
        var deadLetterJobs = await lifecycleManager.GetDeadLetterJobsAsync(cancellationToken);

        var hasAnyJob = executableJobs.Any(job => string.Equals(job.ExecutionId, _executionId, StringComparison.Ordinal))
            || timerJobs.Any(job => string.Equals(job.ExecutionId, _executionId, StringComparison.Ordinal))
            || deadLetterJobs.Any(job => string.Equals(job.ExecutionId, _executionId, StringComparison.Ordinal));

        if (!hasAnyJob)
            throw new WorkflowEngineObjectNotFoundException($"No jobs found for execution '{_executionId}'", typeof(JobEntity));

        await lifecycleManager.CancelJobsByExecutionAsync(_executionId, cancellationToken);

        return null;
    }
}

