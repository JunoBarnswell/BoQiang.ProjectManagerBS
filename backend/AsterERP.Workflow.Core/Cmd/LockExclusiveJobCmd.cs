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

public class LockExclusiveJobCmd : ICommand<object?>
{
    private readonly string _jobId;
    private readonly string _lockOwner;
    private readonly DateTime _lockExpirationTime;

    public LockExclusiveJobCmd(string jobId, string lockOwner, DateTime lockExpirationTime)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new WorkflowEngineArgumentException("jobId is null");
        if (string.IsNullOrWhiteSpace(lockOwner))
            throw new WorkflowEngineArgumentException("lockOwner is null");
        if (lockExpirationTime == default)
            throw new WorkflowEngineArgumentException("lockExpirationTime is not set");

        _jobId = jobId;
        _lockOwner = lockOwner;
        _lockExpirationTime = lockExpirationTime;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager is not IJobLifecycleManager lifecycleManager)
            throw new WorkflowEngineException("Current job manager does not support lifecycle operations for locking jobs.");

        var job = await jobManager.GetJobAsync(_jobId, cancellationToken);
        if (job == null)
            throw new WorkflowEngineObjectNotFoundException($"No executable job found with id '{_jobId}'", typeof(JobEntity));

        var locked = await lifecycleManager.LockJobAsync(_jobId, _lockOwner, _lockExpirationTime, cancellationToken);
        if (!locked)
            throw new WorkflowEngineException($"Unable to lock job '{_jobId}'.");

        return null;
    }
}

