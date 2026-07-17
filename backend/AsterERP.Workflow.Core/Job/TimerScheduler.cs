namespace AsterERP.Workflow.Core.Job;

public sealed class TimerScheduler : IDisposable
{
    private readonly IJobManager _jobManager;
    private readonly IAsyncJobExecutor _jobExecutor;
    private readonly object _lock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _pollingTask;
    private bool _isRunning;

    public TimerScheduler(IJobManager jobManager, IAsyncJobExecutor jobExecutor)
    {
        _jobManager = jobManager ?? throw new ArgumentNullException(nameof(jobManager));
        _jobExecutor = jobExecutor ?? throw new ArgumentNullException(nameof(jobExecutor));
    }

    public void Start(TimeSpan pollInterval, CancellationToken cancellationToken = default)
    {
        if (pollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "Poll interval must be positive.");

        lock (_lock)
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pollingTask = PollTimerJobsAsync(pollInterval, _cancellationTokenSource.Token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cancellationTokenSource;

        lock (_lock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            cancellationTokenSource = _cancellationTokenSource;
            _cancellationTokenSource = null;
            _pollingTask = null;
        }

        cancellationTokenSource?.Cancel();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? pollingTask;
        CancellationTokenSource? cancellationTokenSource;

        lock (_lock)
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            pollingTask = _pollingTask;
            _pollingTask = null;
            cancellationTokenSource = _cancellationTokenSource;
            _cancellationTokenSource = null;
        }

        cancellationTokenSource?.Cancel();

        try
        {
            if (pollingTask != null)
                await pollingTask.WaitAsync(cancellationToken);
        }
        finally
        {
            cancellationTokenSource?.Dispose();
        }
    }

    private async Task PollTimerJobsAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(pollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await PollTimerJobsOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
        }
    }

    private async Task PollTimerJobsOnceAsync(CancellationToken cancellationToken)
    {
        var now = AbpTimeIdProvider.UtcNow;
        _ = await _jobManager.GetJobAsync("due-check", cancellationToken);
        var timerJob = await _jobManager.GetJobAsync("timer-poll", cancellationToken);
        if (timerJob == null || timerJob.DueDate > now)
            return;

        var dueJob = CreateDueJob(timerJob);
        await _jobExecutor.ScheduleJobAsync(dueJob, cancellationToken);
    }

    private static JobEntity CreateDueJob(TimerJobEntity timerJob)
    {
        return new JobEntity
        {
            JobType = AbstractJobEntity.JobTypeTimer,
            ExecutionId = timerJob.ExecutionId,
            ProcessInstanceId = timerJob.ProcessInstanceId,
            ProcessDefinitionId = timerJob.ProcessDefinitionId,
            HandlerType = timerJob.HandlerType,
            HandlerConfiguration = timerJob.HandlerConfiguration,
            DueDate = timerJob.DueDate,
            Repeat = timerJob.Repeat,
            TenantId = timerJob.TenantId,
            Retries = timerJob.Retries
        };
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
