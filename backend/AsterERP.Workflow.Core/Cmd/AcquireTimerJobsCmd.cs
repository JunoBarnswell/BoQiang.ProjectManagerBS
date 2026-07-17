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

public class AcquireTimerJobsCmd : ICommand<AcquiredJobs>
{
    private readonly int _maxJobsPerAcquisition;
    private readonly string _lockOwner;
    private readonly int _lockTimeInMillis;

    public AcquireTimerJobsCmd(int maxJobsPerAcquisition, string lockOwner, int lockTimeInMillis)
    {
        if (maxJobsPerAcquisition <= 0)
            throw new WorkflowEngineArgumentException(
                $"The max jobs per acquisition must be greater than zero, but '{maxJobsPerAcquisition}' has been provided.");
        if (string.IsNullOrWhiteSpace(lockOwner))
            throw new WorkflowEngineArgumentException("lockOwner is null");
        if (lockTimeInMillis <= 0)
            throw new WorkflowEngineArgumentException(
                $"The lock time must be greater than zero, but '{lockTimeInMillis}' has been provided.");

        _maxJobsPerAcquisition = maxJobsPerAcquisition;
        _lockOwner = lockOwner;
        _lockTimeInMillis = lockTimeInMillis;
    }


    public async Task<AcquiredJobs> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var acquiredJobs = new AcquiredJobs();
        if (context.ProcessEngineConfiguration.JobManager is not IJobLifecycleManager lifecycleManager)
            throw new WorkflowEngineException("Current job manager does not support lifecycle operations for acquiring timer jobs.");

        var jobs = await lifecycleManager.AcquireTimerJobsAsync(
            _maxJobsPerAcquisition,
            _lockOwner,
            TimeSpan.FromMilliseconds(_lockTimeInMillis),
            cancellationToken);

        var jobIds = jobs.Select(job => job.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToList();
        if (jobIds.Count > 0)
            acquiredJobs.AddJobIdBatch(jobIds);

        return acquiredJobs;
    }
}

