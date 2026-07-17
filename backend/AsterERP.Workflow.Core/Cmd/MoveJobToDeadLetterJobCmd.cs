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

public class MoveJobToDeadLetterJobCmd : ICommand<object?>
{
    private readonly string _jobId;

    public MoveJobToDeadLetterJobCmd(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new WorkflowEngineArgumentException("jobId is null");

        _jobId = jobId;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager is not IJobLifecycleManager lifecycleManager)
            throw new WorkflowEngineException("Current job manager does not support lifecycle operations for moving job to dead letter.");

        var movedJob = await lifecycleManager.MoveJobToDeadLetterAsync(_jobId, null, cancellationToken);
        if (movedJob == null)
            throw new WorkflowEngineObjectNotFoundException($"No executable job found with id '{_jobId}'", typeof(JobEntity));

        return null;
    }
}

