using System.Collections.Concurrent;
using System.Threading.Channels;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Job;

public class DefaultAsyncJobExecutor : IAsyncExecutor, IDisposable
{
    private readonly IJobManager _jobManager;
    private readonly IEventDispatcher? _eventDispatcher;
    private readonly IProcessEngineConfiguration? _processEngineConfiguration;
    private readonly IFailedJobCommandFactory _failedJobCommandFactory;

    private CancellationTokenSource? _cts;
    private readonly Channel<JobEntity> _jobChannel;
    private readonly ConcurrentDictionary<string, JobEntity> _activeJobs = new();
    private readonly ConcurrentDictionary<string, JobEntity> _deadLetterJobs = new();
    private readonly ConcurrentQueue<JobEntity> _temporaryJobQueue = new();

    private Task? _timerAcquisitionTask;
    private Task? _asyncJobAcquisitionTask;
    private Task? _resetExpiredJobsTask;
    private Task? _channelProcessingTask;

    private int _queueSize = 100;
    private long _secondsToWaitOnShutdown = 60L;

    public bool IsActive { get; private set; }
    public bool IsAutoActivate { get; set; }
    public bool IsMessageQueueMode { get; set; }

    public string LockOwner { get; set; } = AbpTimeIdProvider.NewGuid();
    public int TimerLockTimeInMillis { get; set; } = 5 * 60 * 1000;
    public int AsyncJobLockTimeInMillis { get; set; } = 5 * 60 * 1000;
    public int DefaultTimerJobAcquireWaitTimeInMillis { get; set; } = 10 * 1000;
    public int DefaultAsyncJobAcquireWaitTimeInMillis { get; set; } = 10 * 1000;
    public int DefaultQueueSizeFullWaitTimeInMillis { get; set; }
    public int MaxAsyncJobsDuePerAcquisition { get; set; } = 1;
    public int MaxTimerJobsPerAcquisition { get; set; } = 1;
    public int RetryWaitTimeInMillis { get; set; } = 500;
    public int ResetExpiredJobsInterval { get; set; } = 60 * 1000;
    public int ResetExpiredJobsPageSize { get; set; } = 3;

    public IExecuteAsyncRunnableFactory? ExecuteAsyncRunnableFactory { get; set; }

    public DefaultAsyncJobExecutor(
        IJobManager jobManager,
        IEventDispatcher? eventDispatcher = null,
        IProcessEngineConfiguration? processEngineConfiguration = null,
        IFailedJobCommandFactory? failedJobCommandFactory = null)
    {
        _jobManager = jobManager;
        _eventDispatcher = eventDispatcher;
        _processEngineConfiguration = processEngineConfiguration;
        _failedJobCommandFactory = failedJobCommandFactory ?? new DefaultFailedJobCommandFactory();
        _jobChannel = Channel.CreateBounded<JobEntity>(new BoundedChannelOptions(_queueSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsActive)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsActive = true;

        _timerAcquisitionTask = RunAcquireTimerJobsAsync(_cts.Token);
        _resetExpiredJobsTask = RunResetExpiredJobsAsync(_cts.Token);
        _channelProcessingTask = ProcessChannelAsync(_cts.Token);

        if (!IsMessageQueueMode)
        {
            _asyncJobAcquisitionTask = RunAcquireAsyncJobsAsync(_cts.Token);
        }

        await ExecuteTemporaryJobsAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsActive)
            return;

        IsActive = false;
        _cts?.Cancel();
        _jobChannel.Writer.TryComplete();

        var tasks = new List<Task>();
        if (_timerAcquisitionTask != null) tasks.Add(_timerAcquisitionTask);
        if (_asyncJobAcquisitionTask != null) tasks.Add(_asyncJobAcquisitionTask);
        if (_resetExpiredJobsTask != null) tasks.Add(_resetExpiredJobsTask);
        if (_channelProcessingTask != null) tasks.Add(_channelProcessingTask);

        try
        {
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(_secondsToWaitOnShutdown), cancellationToken);
        }
        catch
        {
        }

        _cts?.Dispose();
        _cts = null;
    }

    public bool ExecuteAsyncJob(JobEntity job)
    {
        if (IsMessageQueueMode)
            return true;

        if (IsActive)
        {
            var runnable = CreateRunnableForJob(job);
            try
            {
                _jobChannel.Writer.TryWrite(job);
            }
            catch
            {
                return false;
            }
        }
        else
        {
            _temporaryJobQueue.Enqueue(job);
        }

        return true;
    }

    private ExecuteAsyncRunnable CreateRunnableForJob(JobEntity job)
    {
        if (ExecuteAsyncRunnableFactory != null && _processEngineConfiguration != null)
        {
            return ExecuteAsyncRunnableFactory.Create(job, _processEngineConfiguration);
        }

        return new ExecuteAsyncRunnable(
            job,
            _jobManager,
            _eventDispatcher,
            _failedJobCommandFactory,
            _processEngineConfiguration,
            RetryWaitTimeInMillis);
    }

    private async Task ExecuteTemporaryJobsAsync()
    {
        while (_temporaryJobQueue.TryDequeue(out var job))
        {
            ExecuteAsyncJob(job);
        }
    }

    private async Task RunAcquireTimerJobsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var now = AbpTimeIdProvider.UtcNow;
                var dueJobs = await FindDueTimerJobsAsync(MaxTimerJobsPerAcquisition, cancellationToken);

                foreach (var job in dueJobs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    JobEntity? executableJob = null;
                    if (_jobManager is IJobLifecycleManager lifecycleManager)
                    {
                        executableJob = await lifecycleManager.MoveTimerToExecutableJobAndGetAsync(job.Id!, cancellationToken);
                    }
                    else
                    {
                        await _jobManager.MoveTimerToExecutableJobAsync(job.Id!, cancellationToken);
                    }

                    if (executableJob != null)
                        ExecuteAsyncJob(executableJob);
                }

                var waitTime = dueJobs.Count >= MaxTimerJobsPerAcquisition
                    ? 0
                    : DefaultTimerJobAcquireWaitTimeInMillis;

                if (waitTime > 0)
                    await Task.Delay(waitTime, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                try
                {
                    await Task.Delay(DefaultTimerJobAcquireWaitTimeInMillis, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task RunAcquireAsyncJobsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var dueJobs = await FindDueAsyncJobsAsync(MaxAsyncJobsDuePerAcquisition, cancellationToken);

                bool allJobsSuccessfullyOffered = true;
                foreach (var job in dueJobs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!ExecuteAsyncJob(job))
                        allJobsSuccessfullyOffered = false;
                }

                var waitTime = dueJobs.Count >= MaxAsyncJobsDuePerAcquisition
                    ? 0
                    : DefaultAsyncJobAcquireWaitTimeInMillis;

                if (waitTime == 0 && !allJobsSuccessfullyOffered)
                    waitTime = DefaultQueueSizeFullWaitTimeInMillis;

                if (waitTime > 0)
                    await Task.Delay(waitTime, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                try
                {
                    await Task.Delay(DefaultAsyncJobAcquireWaitTimeInMillis, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task RunResetExpiredJobsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var expiredJobs = await FindExpiredJobsAsync(ResetExpiredJobsPageSize, cancellationToken);
                var expiredJobIds = expiredJobs.Select(j => j.Id).Where(id => id != null).Cast<string>().ToList();

                if (expiredJobIds.Count > 0)
                {
                    await ResetExpiredJobsAsync(expiredJobIds, cancellationToken);
                }
            }
            catch
            {
            }

            try
            {
                await Task.Delay(ResetExpiredJobsInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessChannelAsync(CancellationToken cancellationToken)
    {
        await foreach (var job in _jobChannel.Reader.ReadAllAsync(cancellationToken))
        {
            _ = ProcessJobAsync(job, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(JobEntity job, CancellationToken cancellationToken)
    {
        job.State = JobState.Executing;
        job.LockOwner = LockOwner;
        job.LockExpirationTime = AbpTimeIdProvider.UtcNow.AddMilliseconds(AsyncJobLockTimeInMillis);
        _activeJobs[job.Id] = job;

        try
        {
            var runnable = CreateRunnableForJob(job);
            await runnable.RunAsync();
            await HandleJobSuccessAsync(job);
        }
        catch (Exception ex)
        {
            await HandleJobFailureAsync(job, ex.Message);
        }
    }

    private Task HandleJobSuccessAsync(JobEntity job)
    {
        job.State = JobState.Completed;
        job.LockOwner = null;
        job.LockExpirationTime = null;
        _activeJobs.TryRemove(job.Id, out _);

        _eventDispatcher?.DispatchEvent(new WorkflowEventImplementation(
            WorkflowEventType.JOB_EXECUTION_SUCCESS,
            job.ExecutionId,
            job.ProcessInstanceId,
            job.ProcessDefinitionId));

        return Task.CompletedTask;
    }

    private async Task HandleJobFailureAsync(JobEntity job, string errorMessage)
    {
        if (string.IsNullOrEmpty(job.Id))
        {
            _eventDispatcher?.DispatchEvent(new WorkflowEventImplementation(
                WorkflowEventType.JOB_EXECUTION_FAILURE,
                job.ExecutionId,
                job.ProcessInstanceId,
                job.ProcessDefinitionId));
            return;
        }

        job.ExceptionMessage = errorMessage;
        job.LockOwner = null;
        job.LockExpirationTime = null;
        _activeJobs.TryRemove(job.Id, out _);

        if (_processEngineConfiguration?.CommandExecutor != null)
        {
            var failedJobListener = new FailedJobListener(
                _processEngineConfiguration.CommandExecutor,
                job,
                _failedJobCommandFactory,
                _eventDispatcher);
            await failedJobListener.OnFailureAsync(new Exception(errorMessage));
            return;
        }

        await HandleJobFailureFallbackAsync(job, errorMessage);
    }

    private async Task HandleJobFailureFallbackAsync(JobEntity job, string errorMessage)
    {
        var storedJob = await _jobManager.GetJobAsync(job.Id!);
        job.Retries = Math.Max(0, (storedJob?.Retries ?? job.Retries) - 1);
        await _jobManager.SetJobRetriesAsync(job.Id!, job.Retries);

        if (_jobManager is IJobLifecycleManager lifecycleManager)
        {
            if (job.Retries <= 0)
            {
                job.State = JobState.DeadLetter;
                await lifecycleManager.MoveJobToDeadLetterAsync(job.Id!, errorMessage);
            }
            else
            {
                job.State = JobState.Created;
                job.DueDate = AbpTimeIdProvider.UtcNow.AddMilliseconds(RetryWaitTimeInMillis);
                if (storedJob != null)
                {
                    storedJob.DueDate = job.DueDate;
                    storedJob.State = job.State;
                }
                await lifecycleManager.UnlockJobAsync(job.Id!);
            }
        }
        else
        {
            if (job.Retries > 0)
            {
                job.State = JobState.Created;
                job.DueDate = AbpTimeIdProvider.UtcNow.AddMilliseconds(RetryWaitTimeInMillis);
            }
            else
            {
                job.State = JobState.DeadLetter;
            }
        }

        _eventDispatcher?.DispatchEvent(new WorkflowEventImplementation(
            WorkflowEventType.JOB_EXECUTION_FAILURE,
            job.ExecutionId,
            job.ProcessInstanceId,
            job.ProcessDefinitionId));
    }

    private Task<List<TimerJobEntity>> FindDueTimerJobsAsync(int maxCount, CancellationToken cancellationToken)
    {
        if (_jobManager is not IJobLifecycleManager lifecycleManager)
            return Task.FromResult(new List<TimerJobEntity>());

        return FindDueTimerJobsCoreAsync(lifecycleManager, maxCount, cancellationToken);
    }

    private async Task<List<TimerJobEntity>> FindDueTimerJobsCoreAsync(
        IJobLifecycleManager lifecycleManager,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var jobs = await lifecycleManager.AcquireTimerJobsAsync(
            maxCount,
            LockOwner,
            TimeSpan.FromMilliseconds(TimerLockTimeInMillis),
            cancellationToken);

        return jobs.ToList();
    }

    private Task<List<JobEntity>> FindDueAsyncJobsAsync(int maxCount, CancellationToken cancellationToken)
    {
        if (_jobManager is not IJobLifecycleManager lifecycleManager)
            return Task.FromResult(new List<JobEntity>());

        return FindDueAsyncJobsCoreAsync(lifecycleManager, maxCount, cancellationToken);
    }

    private async Task<List<JobEntity>> FindDueAsyncJobsCoreAsync(
        IJobLifecycleManager lifecycleManager,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var jobs = await lifecycleManager.AcquireJobsAsync(
            maxCount,
            LockOwner,
            TimeSpan.FromMilliseconds(AsyncJobLockTimeInMillis),
            cancellationToken);

        return jobs.ToList();
    }

    private Task<List<JobEntity>> FindExpiredJobsAsync(int pageSize, CancellationToken cancellationToken)
    {
        if (_jobManager is not IJobLifecycleManager lifecycleManager)
            return Task.FromResult(new List<JobEntity>());

        return FindExpiredJobsCoreAsync(lifecycleManager, pageSize, cancellationToken);
    }

    private static async Task<List<JobEntity>> FindExpiredJobsCoreAsync(
        IJobLifecycleManager lifecycleManager,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var jobs = await lifecycleManager.FindExpiredJobsAsync(pageSize, cancellationToken);
        return jobs.ToList();
    }

    private Task ResetExpiredJobsAsync(List<string> jobIds, CancellationToken cancellationToken)
    {
        if (_jobManager is IJobLifecycleManager lifecycleManager)
            return lifecycleManager.ResetExpiredJobsAsync(jobIds, cancellationToken);

        foreach (var jobId in jobIds)
        {
            if (_activeJobs.TryGetValue(jobId, out var job))
            {
                job.LockOwner = null;
                job.LockExpirationTime = null;
                job.State = JobState.Created;
            }
        }

        return Task.CompletedTask;
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

public class ManagedAsyncJobExecutor : DefaultAsyncJobExecutor
{
    public ManagedAsyncJobExecutor(
        IJobManager jobManager,
        IEventDispatcher? eventDispatcher = null,
        IProcessEngineConfiguration? processEngineConfiguration = null,
        IFailedJobCommandFactory? failedJobCommandFactory = null)
        : base(jobManager, eventDispatcher, processEngineConfiguration, failedJobCommandFactory)
    {
    }
}

public class ExecuteAsyncRunnable
{
    private readonly JobEntity _job;
    private readonly IJobManager _jobManager;
    private readonly IEventDispatcher? _eventDispatcher;
    private readonly IFailedJobCommandFactory _failedJobCommandFactory;
    private readonly IProcessEngineConfiguration? _processEngineConfiguration;
    private readonly int _retryWaitTimeInMillis;

    public ExecuteAsyncRunnable(
        JobEntity job,
        IJobManager jobManager,
        IEventDispatcher? eventDispatcher = null,
        IFailedJobCommandFactory? failedJobCommandFactory = null,
        IProcessEngineConfiguration? processEngineConfiguration = null,
        int retryWaitTimeInMillis = 60 * 1000)
    {
        _job = job;
        _jobManager = jobManager;
        _eventDispatcher = eventDispatcher;
        _failedJobCommandFactory = failedJobCommandFactory ?? new DefaultFailedJobCommandFactory();
        _processEngineConfiguration = processEngineConfiguration;
        _retryWaitTimeInMillis = retryWaitTimeInMillis;
    }

    public async Task RunAsync()
    {
        bool lockNotNeededOrSuccess = await LockJobIfNeededAsync();

        if (lockNotNeededOrSuccess)
        {
            await ExecuteJobAsync();
            await UnlockJobIfNeededAsync();
        }
    }

    private async Task<bool> LockJobIfNeededAsync()
    {
        if (_job.IsExclusive)
        {
            _job.LockOwner = Environment.MachineName;
            _job.LockExpirationTime = AbpTimeIdProvider.UtcNow.AddMinutes(5);
        }

        return true;
    }

    private async Task ExecuteJobAsync()
    {
        try
        {
            if (_processEngineConfiguration?.CommandExecutor != null)
            {
                await _processEngineConfiguration.CommandExecutor.ExecuteAsync(new ExecuteAsyncJobCmd(_job));
            }
            else
            {
                await _jobManager.ExecuteJobAsync(_job.Id!);
            }
        }
        catch (Exception ex)
        {
            await HandleFailedJobAsync(ex);
        }
    }

    private Task UnlockJobIfNeededAsync()
    {
        if (_job.IsExclusive)
        {
            _job.LockOwner = null;
            _job.LockExpirationTime = null;
        }

        return Task.CompletedTask;
    }

    private async Task HandleFailedJobAsync(Exception exception)
    {
        _job.ExceptionMessage = exception.Message;
        if (!string.IsNullOrEmpty(_job.Id) && _processEngineConfiguration?.CommandExecutor != null)
        {
            var failedJobListener = new FailedJobListener(
                _processEngineConfiguration.CommandExecutor,
                _job,
                _failedJobCommandFactory,
                _eventDispatcher);
            await failedJobListener.OnFailureAsync(exception);
            return;
        }

        await HandleFailedJobFallbackAsync(exception);
    }

    private async Task HandleFailedJobFallbackAsync(Exception exception)
    {
        if (string.IsNullOrEmpty(_job.Id))
        {
            _eventDispatcher?.DispatchEvent(new WorkflowEventImplementation(
                WorkflowEventType.JOB_EXECUTION_FAILURE,
                _job.ExecutionId,
                _job.ProcessInstanceId,
                _job.ProcessDefinitionId));
            return;
        }

        _job.LockOwner = null;
        _job.LockExpirationTime = null;
        var storedJob = await _jobManager.GetJobAsync(_job.Id);
        _job.Retries = Math.Max(0, (storedJob?.Retries ?? _job.Retries) - 1);
        await _jobManager.SetJobRetriesAsync(_job.Id!, _job.Retries);

        if (_jobManager is IJobLifecycleManager lifecycleManager)
        {
            if (_job.Retries <= 0)
            {
                _job.State = JobState.DeadLetter;
                await lifecycleManager.MoveJobToDeadLetterAsync(_job.Id, exception.Message);
            }
            else
            {
                _job.State = JobState.Created;
                _job.DueDate = AbpTimeIdProvider.UtcNow.AddMilliseconds(_retryWaitTimeInMillis);
                if (storedJob != null)
                {
                    storedJob.DueDate = _job.DueDate;
                    storedJob.State = _job.State;
                }
                await lifecycleManager.UnlockJobAsync(_job.Id);
            }
        }
        else
        {
            if (_job.Retries > 0)
            {
                _job.State = JobState.Created;
                _job.DueDate = AbpTimeIdProvider.UtcNow.AddMilliseconds(_retryWaitTimeInMillis);
            }
            else
            {
                _job.State = JobState.DeadLetter;
            }
        }

        _eventDispatcher?.DispatchEvent(new WorkflowEventImplementation(
            WorkflowEventType.JOB_EXECUTION_FAILURE,
            _job.ExecutionId,
            _job.ProcessInstanceId,
            _job.ProcessDefinitionId));
    }
}

public interface IExecuteAsyncRunnableFactory
{
    ExecuteAsyncRunnable Create(JobEntity job, IProcessEngineConfiguration processEngineConfiguration);
}

public class AcquiredJobEntities
{
    private readonly Dictionary<string, JobEntity> _acquiredJobs = new();

    public void AddJob(JobEntity job)
    {
        if (job.Id != null)
            _acquiredJobs[job.Id] = job;
    }

    public ICollection<JobEntity> Jobs => _acquiredJobs.Values;

    public bool Contains(string jobId)
    {
        return _acquiredJobs.ContainsKey(jobId);
    }

    public int Size => _acquiredJobs.Count;
}

public class AcquiredTimerJobEntities
{
    private readonly Dictionary<string, TimerJobEntity> _acquiredJobs = new();

    public void AddJob(TimerJobEntity job)
    {
        if (job.Id != null)
            _acquiredJobs[job.Id] = job;
    }

    public ICollection<TimerJobEntity> Jobs => _acquiredJobs.Values;

    public bool Contains(string jobId)
    {
        return _acquiredJobs.ContainsKey(jobId);
    }

    public int Size => _acquiredJobs.Count;
}

public class FindExpiredJobsCmd : ICommand<List<JobEntity>>
{
    private readonly int _pageSize;

    public FindExpiredJobsCmd(int pageSize)
    {
        _pageSize = pageSize;
    }


    public async Task<List<JobEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (context.ProcessEngineConfiguration.JobManager is not IJobLifecycleManager lifecycleManager)
            return new List<JobEntity>();

        var jobs = await lifecycleManager.FindExpiredJobsAsync(_pageSize, cancellationToken);
        return jobs.ToList();
    }
}

public class ResetExpiredJobsCmd : ICommand<object?>
{
    private readonly ICollection<string> _jobIds;

    public ResetExpiredJobsCmd(ICollection<string> jobIds)
    {
        _jobIds = jobIds;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (context.ProcessEngineConfiguration.JobManager is IJobLifecycleManager lifecycleManager)
            await lifecycleManager.ResetExpiredJobsAsync(_jobIds, cancellationToken);

        return null;
    }
}



