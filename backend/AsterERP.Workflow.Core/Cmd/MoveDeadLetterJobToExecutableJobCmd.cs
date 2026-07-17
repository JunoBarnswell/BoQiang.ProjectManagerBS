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

public class MoveDeadLetterJobToExecutableJobCmd : ICommand<JobEntity?>
{
    private readonly string _jobId;
    private readonly int _retries;

    public MoveDeadLetterJobToExecutableJobCmd(string jobId, int retries)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new WorkflowEngineArgumentException("jobId is null");
        if (retries < 0)
            throw new WorkflowEngineArgumentException(
                $"The number of job retries must be a non-negative Integer, but '{retries}' has been provided.");
        _jobId = jobId;
        _retries = retries;
    }


    public async Task<JobEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager is not IJobLifecycleManager lifecycleManager)
            throw new WorkflowEngineException("Current job manager does not support lifecycle operations for moving dead letter jobs.");

        var movedJob = await lifecycleManager.MoveDeadLetterJobToExecutableAsync(_jobId, _retries, cancellationToken);
        if (movedJob == null)
            throw new WorkflowEngineObjectNotFoundException($"No dead letter job found with id '{_jobId}'", typeof(JobEntity));

        return movedJob;
    }
}

