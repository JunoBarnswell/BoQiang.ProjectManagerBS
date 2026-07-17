using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Job;

public interface IAsyncExecutor
{
    bool IsActive { get; }
    bool IsAutoActivate { get; set; }
    string LockOwner { get; }
    int TimerLockTimeInMillis { get; set; }
    int AsyncJobLockTimeInMillis { get; set; }
    int DefaultTimerJobAcquireWaitTimeInMillis { get; set; }
    int DefaultAsyncJobAcquireWaitTimeInMillis { get; set; }
    int DefaultQueueSizeFullWaitTimeInMillis { get; set; }
    int MaxAsyncJobsDuePerAcquisition { get; set; }
    int MaxTimerJobsPerAcquisition { get; set; }
    int RetryWaitTimeInMillis { get; set; }
    int ResetExpiredJobsInterval { get; set; }
    int ResetExpiredJobsPageSize { get; set; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    bool ExecuteAsyncJob(JobEntity job);
}
