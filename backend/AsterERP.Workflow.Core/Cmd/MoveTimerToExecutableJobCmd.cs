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

public class MoveTimerToExecutableJobCmd : ICommand<JobEntity?>
{
    private readonly string _timerJobId;

    public MoveTimerToExecutableJobCmd(string timerJobId)
    {
        if (string.IsNullOrWhiteSpace(timerJobId))
            throw new WorkflowEngineArgumentException("timerJobId is null");

        _timerJobId = timerJobId;
    }


    public async Task<JobEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager is not IJobLifecycleManager lifecycleManager)
            throw new WorkflowEngineException("Current job manager does not support lifecycle operations for moving timer jobs.");

        var movedJob = await lifecycleManager.MoveTimerToExecutableJobAndGetAsync(_timerJobId, cancellationToken);
        if (movedJob == null)
            throw new WorkflowEngineObjectNotFoundException($"No timer job found with id '{_timerJobId}'", typeof(JobEntity));

        return movedJob;
    }
}

