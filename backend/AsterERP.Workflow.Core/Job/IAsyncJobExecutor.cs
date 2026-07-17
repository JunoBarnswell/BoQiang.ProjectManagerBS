namespace AsterERP.Workflow.Core.Job;

public interface IAsyncJobExecutor
{
    bool IsRunning { get; }
    int ActiveJobCount { get; }
    long TotalJobsExecuted { get; }
    TimeSpan PollInterval { get; set; }
    int MaxJobsPerAcquisition { get; set; }

    global::System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task ScheduleJobAsync(JobEntity job, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task MoveJobToDeadLetterAsync(string jobId, string? errorMessage, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task MoveDeadLetterJobToActiveAsync(string jobId, int retries, CancellationToken cancellationToken = default);
}
