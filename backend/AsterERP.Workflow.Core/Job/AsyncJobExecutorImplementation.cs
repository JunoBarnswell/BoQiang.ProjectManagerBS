using System.Collections.Concurrent;
using System.Threading.Channels;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Job;

public class AsyncJobExecutorImplementation : IAsyncJobExecutor, IDisposable
{
    private readonly IJobManager _jobManager;
    private readonly IEventDispatcher? _eventDispatcher;
    private readonly JobRetryStrategy _retryStrategy;
    private CancellationTokenSource? _cts;
    private readonly Channel<JobEntity> _jobChannel;
    private readonly ConcurrentDictionary<string, JobEntity> _knownJobs = new();
    private readonly ConcurrentDictionary<string, JobEntity> _activeJobs = new();
    private readonly ConcurrentDictionary<string, JobEntity> _deadLetterJobs = new();
    private int _activeJobCount;
    private long _totalJobsExecuted;
    private readonly object _lock = new();

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
    public int ActiveJobCount => _activeJobCount;
    public long TotalJobsExecuted => _totalJobsExecuted;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxJobsPerAcquisition { get; set; } = 5;

    public AsyncJobExecutorImplementation(
        IJobManager jobManager,
        IEventDispatcher? eventDispatcher = null,
        JobRetryStrategy? retryStrategy = null)
    {
        _jobManager = jobManager;
        _eventDispatcher = eventDispatcher;
        _retryStrategy = retryStrategy ?? new JobRetryStrategy();
        _jobChannel = Channel.CreateBounded<JobEntity>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public async global::System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (IsRunning)
                return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        _ = PollAndExecuteAsync(_cts.Token);
        _ = ProcessChannelAsync(_cts.Token);

        await global::System.Threading.Tasks.Task.CompletedTask;
    }

    public async global::System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts;
        lock (_lock)
        {
            cts = _cts;
            _cts = null;
        }

        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _jobChannel.Writer.TryComplete();
        await global::System.Threading.Tasks.Task.CompletedTask;
    }

    public async global::System.Threading.Tasks.Task ScheduleJobAsync(JobEntity job, CancellationToken cancellationToken = default)
    {
        job.State = JobState.Created;
        _knownJobs[job.Id] = job;
        _activeJobs[job.Id] = job;

        if (job.DueDate == null || job.DueDate <= AbpTimeIdProvider.UtcNow)
        {
            await _jobChannel.Writer.WriteAsync(job, cancellationToken);
        }
    }

    public async global::System.Threading.Tasks.Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.State = JobState.Acquired;
            await _jobChannel.Writer.WriteAsync(job, cancellationToken);
        }
    }

    public global::System.Threading.Tasks.Task MoveJobToDeadLetterAsync(string jobId, string? errorMessage, CancellationToken cancellationToken = default)
    {
        if (_activeJobs.TryRemove(jobId, out var job) || _knownJobs.TryGetValue(jobId, out job))
        {
            job.State = JobState.DeadLetter;
            job.ExceptionMessage = errorMessage;
            _deadLetterJobs[jobId] = job;
        }

        return global::System.Threading.Tasks.Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task MoveDeadLetterJobToActiveAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        if (_deadLetterJobs.TryRemove(jobId, out var job))
        {
            job.State = JobState.Created;
            job.Retries = retries;
            job.ExceptionMessage = null;
            _activeJobs[jobId] = job;
        }

        return global::System.Threading.Tasks.Task.CompletedTask;
    }

    private async global::System.Threading.Tasks.Task PollAndExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollDueJobsAsync(cancellationToken);
                await global::System.Threading.Tasks.Task.Delay(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async global::System.Threading.Tasks.Task PollDueJobsAsync(CancellationToken cancellationToken)
    {
        var now = AbpTimeIdProvider.UtcNow;
        var dueJobs = _activeJobs.Values
            .Where(j => j.State == JobState.Created && (j.DueDate == null || j.DueDate <= now))
            .Take(MaxJobsPerAcquisition)
            .ToList();

        foreach (var job in dueJobs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            job.State = JobState.Acquired;
            await _jobChannel.Writer.WriteAsync(job, cancellationToken);
        }
    }

    private async global::System.Threading.Tasks.Task ProcessChannelAsync(CancellationToken cancellationToken)
    {
        await foreach (var job in _jobChannel.Reader.ReadAllAsync(cancellationToken))
        {
            _ = ProcessJobAsync(job, cancellationToken);
        }
    }

    private async global::System.Threading.Tasks.Task ProcessJobAsync(JobEntity job, CancellationToken cancellationToken)
    {
        if (!_activeJobs.TryGetValue(job.Id, out var activeJob) || activeJob.State == JobState.DeadLetter)
        {
            return;
        }

        Interlocked.Increment(ref _activeJobCount);
        job.State = JobState.Executing;
        job.LockOwner = Environment.MachineName;
        job.LockExpirationTime = AbpTimeIdProvider.UtcNow.AddMinutes(5);

        try
        {
            await _jobManager.ExecuteJobAsync(job.Id, cancellationToken);
            await HandleJobSuccessAsync(job);
        }
        catch (Exception ex)
        {
            await HandleJobFailureAsync(job, ex.Message);
        }
        finally
        {
            Interlocked.Decrement(ref _activeJobCount);
        }
    }

    private global::System.Threading.Tasks.Task HandleJobSuccessAsync(JobEntity job)
    {
        if (!_activeJobs.TryRemove(job.Id, out var activeJob) || activeJob.State == JobState.DeadLetter)
        {
            return global::System.Threading.Tasks.Task.CompletedTask;
        }

        job.State = JobState.Completed;
        job.LockOwner = null;
        job.LockExpirationTime = null;
        Interlocked.Increment(ref _totalJobsExecuted);

        _eventDispatcher?.DispatchEvent(new WorkflowEventImplementation(
            WorkflowEventType.JOB_EXECUTION_SUCCESS,
            job.ExecutionId,
            job.ProcessInstanceId,
            job.ProcessDefinitionId));

        return global::System.Threading.Tasks.Task.CompletedTask;
    }

    private global::System.Threading.Tasks.Task HandleJobFailureAsync(JobEntity job, string errorMessage)
    {
        job.ExceptionMessage = errorMessage;
        job.LockOwner = null;
        job.LockExpirationTime = null;
        job.Retries--;

        if (job.Retries > 0)
        {
            job.State = JobState.Created;
            var nextDue = _retryStrategy.CalculateNextDueDate(_retryStrategy.DefaultRetries - job.Retries);
            job.DueDate = nextDue ?? AbpTimeIdProvider.UtcNow.AddMinutes(1);
        }
        else
        {
            job.State = JobState.DeadLetter;
            _activeJobs.TryRemove(job.Id, out _);
            _deadLetterJobs[job.Id] = job;
        }

        _eventDispatcher?.DispatchEvent(new WorkflowEventImplementation(
            WorkflowEventType.JOB_EXECUTION_FAILURE,
            job.ExecutionId,
            job.ProcessInstanceId,
            job.ProcessDefinitionId));

        return global::System.Threading.Tasks.Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        _jobChannel.Writer.TryComplete();
        GC.SuppressFinalize(this);
    }
}

