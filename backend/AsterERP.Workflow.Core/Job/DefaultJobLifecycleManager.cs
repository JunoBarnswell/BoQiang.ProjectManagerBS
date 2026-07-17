using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Job;

public class DefaultJobLifecycleManager : IJobManager, IJobLifecycleManager
{
    private readonly IJobManager _innerJobManager;
    private readonly ConcurrentDictionary<string, JobEntity> _executableJobs = new();
    private readonly ConcurrentDictionary<string, TimerJobEntity> _timerJobs = new();
    private readonly ConcurrentDictionary<string, JobEntity> _suspendedJobs = new();
    private readonly ConcurrentDictionary<string, JobEntity> _deadLetterJobs = new();

    public DefaultJobLifecycleManager(IJobManager innerJobManager)
    {
        _innerJobManager = innerJobManager;
    }

    public Task<TimerJobEntity> CreateTimerJobAsync(string executionId, string processInstanceId, string processDefinitionId, DateTime? dueDate, string? repeat, string handlerType, string? handlerConfiguration, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return _innerJobManager.CreateTimerJobAsync(executionId, processInstanceId, processDefinitionId, dueDate, repeat, handlerType, handlerConfiguration, tenantId, cancellationToken);
    }

    public Task ScheduleTimerJobAsync(TimerJobEntity job, CancellationToken cancellationToken = default)
    {
        if (job.Id != null)
            _timerJobs[job.Id] = job;

        return _innerJobManager.ScheduleTimerJobAsync(job, cancellationToken);
    }

    public Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        _executableJobs.TryRemove(jobId, out _);
        return _innerJobManager.ExecuteJobAsync(jobId, cancellationToken);
    }

    public Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        _executableJobs.TryRemove(jobId, out _);
        _timerJobs.TryRemove(jobId, out _);
        _suspendedJobs.TryRemove(jobId, out _);
        _deadLetterJobs.TryRemove(jobId, out _);
        return _innerJobManager.DeleteJobAsync(jobId, cancellationToken);
    }

    public Task<int> SetJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        return _innerJobManager.SetJobRetriesAsync(jobId, retries, cancellationToken);
    }

    public Task<TimerJobEntity?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_timerJobs.TryGetValue(jobId, out var timerJob))
            return Task.FromResult<TimerJobEntity?>(timerJob);

        if (_executableJobs.TryGetValue(jobId, out var execJob))
        {
            var timerEquivalent = new TimerJobEntity
            {
                Id = execJob.Id,
                JobType = execJob.JobType,
                ExecutionId = execJob.ExecutionId,
                ProcessInstanceId = execJob.ProcessInstanceId,
                ProcessDefinitionId = execJob.ProcessDefinitionId,
                DueDate = execJob.DueDate,
                Repeat = execJob.Repeat,
                HandlerType = execJob.HandlerType,
                HandlerConfiguration = execJob.HandlerConfiguration,
                Retries = execJob.Retries,
                ExceptionMessage = execJob.ExceptionMessage,
                TenantId = execJob.TenantId
            };
            return Task.FromResult<TimerJobEntity?>(timerEquivalent);
        }

        return _innerJobManager.GetJobAsync(jobId, cancellationToken);
    }

    public Task CancelTimerJobAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
    {
        return _innerJobManager.CancelTimerJobAsync(executionId, activityId, cancellationToken);
    }

    public Task<TimerJobEntity?> GetTimerJobByExecutionAndActivityAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
    {
        return _innerJobManager.GetTimerJobByExecutionAndActivityAsync(executionId, activityId, cancellationToken);
    }

    public Task<IReadOnlyList<JobEntity>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = _executableJobs.Values.ToList();
        return Task.FromResult<IReadOnlyList<JobEntity>>(jobs);
    }

    public Task<IReadOnlyList<TimerJobEntity>> GetTimerJobsAsync(CancellationToken cancellationToken = default)
    {
        var timerJobs = _timerJobs.Values.ToList();
        return Task.FromResult<IReadOnlyList<TimerJobEntity>>(timerJobs);
    }

    public Task<IReadOnlyList<JobEntity>> GetDeadLetterJobsAsync(CancellationToken cancellationToken = default)
    {
        var deadLetterJobs = _deadLetterJobs.Values.ToList();
        return Task.FromResult<IReadOnlyList<JobEntity>>(deadLetterJobs);
    }

    public Task MoveTimerToExecutableJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_timerJobs.TryRemove(jobId, out var timerJob))
        {
            var executableJob = new JobEntity
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
                Retries = timerJob.Retries,
                State = JobState.Created
            };
            _executableJobs[executableJob.Id] = executableJob;
        }

        return _innerJobManager.MoveTimerToExecutableJobAsync(jobId, cancellationToken);
    }

    public Task<TimerJobEntity> CreateAsyncJobAsync(string executionId, string processInstanceId, string processDefinitionId, bool exclusive, CancellationToken cancellationToken = default)
    {
        return _innerJobManager.CreateAsyncJobAsync(executionId, processInstanceId, processDefinitionId, exclusive, cancellationToken);
    }

    public Task ScheduleAsyncJobAsync(TimerJobEntity job, CancellationToken cancellationToken = default)
    {
        if (job.Id != null)
            _executableJobs[job.Id] = new JobEntity
            {
                Id = job.Id,
                ExecutionId = job.ExecutionId,
                ProcessInstanceId = job.ProcessInstanceId,
                ProcessDefinitionId = job.ProcessDefinitionId,
                HandlerType = job.HandlerType,
                HandlerConfiguration = job.HandlerConfiguration,
                DueDate = job.DueDate,
                Repeat = job.Repeat,
                TenantId = job.TenantId,
                Retries = job.Retries,
                State = JobState.Created
            };

        return _innerJobManager.ScheduleAsyncJobAsync(job, cancellationToken);
    }

    public Task<IReadOnlyList<JobEntity>> AcquireJobsAsync(int maxCount, string lockOwner, TimeSpan lockTime, CancellationToken cancellationToken = default)
    {
        var now = AbpTimeIdProvider.UtcNow;
        var jobs = _executableJobs.Values
            .Where(job => job.State == JobState.Created
                && job.Retries > 0
                && (job.DueDate == null || job.DueDate <= now)
                && (job.LockOwner == null || job.LockExpirationTime <= now))
            .OrderBy(job => job.DueDate ?? DateTime.MinValue)
            .Take(Math.Max(0, maxCount))
            .ToList();

        foreach (var job in jobs)
        {
            job.State = JobState.Acquired;
            job.LockOwner = lockOwner;
            job.LockExpirationTime = now.Add(lockTime);
            job.State = JobState.Acquired;
        }

        return Task.FromResult<IReadOnlyList<JobEntity>>(jobs);
    }

    public Task<IReadOnlyList<TimerJobEntity>> AcquireTimerJobsAsync(int maxCount, string lockOwner, TimeSpan lockTime, CancellationToken cancellationToken = default)
    {
        var now = AbpTimeIdProvider.UtcNow;
        var jobs = _timerJobs.Values
            .Where(job => job.State == JobState.Created
                && job.Retries > 0
                && (job.DueDate == null || job.DueDate <= now)
                && (job.LockOwner == null || job.LockExpirationTime <= now))
            .OrderBy(job => job.DueDate ?? DateTime.MinValue)
            .Take(Math.Max(0, maxCount))
            .ToList();

        foreach (var job in jobs)
        {
            job.LockOwner = lockOwner;
            job.LockExpirationTime = now.Add(lockTime);
        }

        return Task.FromResult<IReadOnlyList<TimerJobEntity>>(jobs);
    }

    public Task<IReadOnlyList<JobEntity>> FindExpiredJobsAsync(int pageSize, CancellationToken cancellationToken = default)
    {
        var now = AbpTimeIdProvider.UtcNow;
        var jobs = _executableJobs.Values
            .Where(job => job.LockExpirationTime != null && job.LockExpirationTime <= now)
            .OrderBy(job => job.LockExpirationTime)
            .Take(Math.Max(0, pageSize))
            .ToList();

        return Task.FromResult<IReadOnlyList<JobEntity>>(jobs);
    }

    public Task ResetExpiredJobsAsync(IEnumerable<string> jobIds, CancellationToken cancellationToken = default)
    {
        foreach (var jobId in jobIds)
        {
            if (_executableJobs.TryGetValue(jobId, out var job))
            {
                job.LockOwner = null;
                job.LockExpirationTime = null;
                if (job.State == JobState.Acquired || job.State == JobState.Executing)
                    job.State = JobState.Created;
            }
        }

        return Task.CompletedTask;
    }

    public async Task<JobEntity?> MoveTimerToExecutableJobAndGetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        JobEntity? executableJob = null;
        if (_timerJobs.TryRemove(jobId, out var timerJob))
        {
            executableJob = new JobEntity
            {
                Id = timerJob.Id ?? AbpTimeIdProvider.NewGuid("N"),
                JobType = AbstractJobEntity.JobTypeTimer,
                ExecutionId = timerJob.ExecutionId,
                ProcessInstanceId = timerJob.ProcessInstanceId,
                ProcessDefinitionId = timerJob.ProcessDefinitionId,
                HandlerType = timerJob.HandlerType,
                HandlerConfiguration = timerJob.HandlerConfiguration,
                DueDate = timerJob.DueDate,
                Repeat = timerJob.Repeat,
                TenantId = timerJob.TenantId,
                Retries = timerJob.Retries,
                ExceptionMessage = timerJob.ExceptionMessage,
                State = JobState.Created,
                LockOwner = timerJob.LockOwner,
                LockExpirationTime = timerJob.LockExpirationTime,
                IsExclusive = timerJob.IsExclusive
            };
            _executableJobs[executableJob.Id] = executableJob;
        }

        await _innerJobManager.MoveTimerToExecutableJobAsync(jobId, cancellationToken);
        return executableJob;
    }

    public Task<JobEntity?> MoveJobToDeadLetterAsync(string jobId, string? exceptionMessage, CancellationToken cancellationToken = default)
    {
        if (!_executableJobs.TryRemove(jobId, out var job))
            return Task.FromResult<JobEntity?>(null);

        job.State = JobState.DeadLetter;
        job.ExceptionMessage = exceptionMessage;
        job.LockOwner = null;
        job.LockExpirationTime = null;
        _deadLetterJobs[jobId] = job;
        return Task.FromResult<JobEntity?>(job);
    }

    public Task<JobEntity?> MoveDeadLetterJobToExecutableAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        if (!_deadLetterJobs.TryRemove(jobId, out var job))
            return Task.FromResult<JobEntity?>(null);

        job.State = JobState.Created;
        job.Retries = retries;
        job.ExceptionMessage = null;
        job.LockOwner = null;
        job.LockExpirationTime = null;
        _executableJobs[jobId] = job;
        return Task.FromResult<JobEntity?>(job);
    }

    public Task<bool> LockJobAsync(string jobId, string lockOwner, DateTime lockExpirationTime, CancellationToken cancellationToken = default)
    {
        if (!_executableJobs.TryGetValue(jobId, out var job))
            return Task.FromResult(false);

        var now = AbpTimeIdProvider.UtcNow;
        if (job.LockOwner != null && job.LockExpirationTime > now)
            return Task.FromResult(false);

        job.LockOwner = lockOwner;
        job.LockExpirationTime = lockExpirationTime;
        job.State = JobState.Acquired;
        return Task.FromResult(true);
    }

    public Task UnlockJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_executableJobs.TryGetValue(jobId, out var job))
        {
            job.LockOwner = null;
            job.LockExpirationTime = null;
            if (job.State == JobState.Acquired || job.State == JobState.Executing)
                job.State = JobState.Created;
        }

        return Task.CompletedTask;
    }

    public Task CancelJobsByExecutionAsync(string executionId, CancellationToken cancellationToken = default)
    {
        foreach (var job in _executableJobs.Values.Where(job => job.ExecutionId == executionId).ToList())
            _executableJobs.TryRemove(job.Id, out _);

        foreach (var job in _timerJobs.Values.Where(job => job.ExecutionId == executionId && job.Id != null).ToList())
            _timerJobs.TryRemove(job.Id!, out _);

        foreach (var job in _deadLetterJobs.Values.Where(job => job.ExecutionId == executionId).ToList())
            _deadLetterJobs.TryRemove(job.Id, out _);

        return Task.CompletedTask;
    }
}

