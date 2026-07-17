using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Job;

public interface IJobManager
{
    global::System.Threading.Tasks.Task<TimerJobEntity> CreateTimerJobAsync(string executionId, string processInstanceId, string processDefinitionId, DateTime? dueDate, string? repeat, string handlerType, string? handlerConfiguration, string? tenantId = null, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task ScheduleTimerJobAsync(TimerJobEntity job, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<int> SetJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<TimerJobEntity?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task CancelTimerJobAsync(string executionId, string activityId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<TimerJobEntity?> GetTimerJobByExecutionAndActivityAsync(string executionId, string activityId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task MoveTimerToExecutableJobAsync(string jobId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<TimerJobEntity> CreateAsyncJobAsync(string executionId, string processInstanceId, string processDefinitionId, bool exclusive, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task ScheduleAsyncJobAsync(TimerJobEntity job, CancellationToken cancellationToken = default);
}

public interface IJobLifecycleManager
{
    global::System.Threading.Tasks.Task<IReadOnlyList<JobEntity>> AcquireJobsAsync(int maxCount, string lockOwner, TimeSpan lockTime, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<IReadOnlyList<TimerJobEntity>> AcquireTimerJobsAsync(int maxCount, string lockOwner, TimeSpan lockTime, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<IReadOnlyList<JobEntity>> FindExpiredJobsAsync(int pageSize, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task ResetExpiredJobsAsync(IEnumerable<string> jobIds, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<JobEntity?> MoveTimerToExecutableJobAndGetAsync(string jobId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<JobEntity?> MoveJobToDeadLetterAsync(string jobId, string? exceptionMessage, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<JobEntity?> MoveDeadLetterJobToExecutableAsync(string jobId, int retries, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> LockJobAsync(string jobId, string lockOwner, DateTime lockExpirationTime, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task UnlockJobAsync(string jobId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task CancelJobsByExecutionAsync(string executionId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<IReadOnlyList<JobEntity>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<JobEntity>>(Array.Empty<JobEntity>());
    }
    global::System.Threading.Tasks.Task<IReadOnlyList<TimerJobEntity>> GetTimerJobsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TimerJobEntity>>(Array.Empty<TimerJobEntity>());
    }
    global::System.Threading.Tasks.Task<IReadOnlyList<JobEntity>> GetDeadLetterJobsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<JobEntity>>(Array.Empty<JobEntity>());
    }
}

public class JobManagerImplementation : IJobManager, IJobLifecycleManager
{
    private readonly ConcurrentDictionary<string, TimerJobEntity> _jobs = new();
    private readonly ConcurrentDictionary<string, TimerJobEntity> _timerJobs = new();
    private readonly ConcurrentDictionary<string, JobEntity> _executableJobs = new();
    private readonly ConcurrentDictionary<string, JobEntity> _deadLetterJobs = new();
    private readonly ConcurrentDictionary<string, List<string>> _executionJobs = new();
    private int _idCounter;

    public global::System.Threading.Tasks.Task<TimerJobEntity> CreateTimerJobAsync(
        string executionId,
        string processInstanceId,
        string processDefinitionId,
        DateTime? dueDate,
        string? repeat,
        string handlerType,
        string? handlerConfiguration,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var job = new TimerJobEntity
        {
            Id = Interlocked.Increment(ref _idCounter).ToString(),
            JobType = AbstractJobEntity.JobTypeTimer,
            ExecutionId = executionId,
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionId = processDefinitionId,
            DueDate = dueDate,
            Repeat = repeat,
            HandlerType = handlerType,
            HandlerConfiguration = handlerConfiguration,
            TenantId = tenantId
        };
        TrackExecutionJob(executionId, job.Id!);
        return Task.FromResult(job);
    }

    public global::System.Threading.Tasks.Task ScheduleTimerJobAsync(TimerJobEntity job, CancellationToken cancellationToken = default)
    {
        if (job.Id != null)
        {
            job.JobType = AbstractJobEntity.JobTypeTimer;
            _jobs[job.Id] = job;
            _timerJobs[job.Id] = job;
            if (job.ExecutionId != null)
                TrackExecutionJob(job.ExecutionId, job.Id);
        }
        return Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            UntrackExecutionJob(job.ExecutionId, jobId);
        }
        else if (_executableJobs.TryGetValue(jobId, out var executableJob))
        {
            UntrackExecutionJob(executableJob.ExecutionId, jobId);
        }

        _jobs.TryRemove(jobId, out _);
        _timerJobs.TryRemove(jobId, out _);
        _executableJobs.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            UntrackExecutionJob(job.ExecutionId, jobId);
        }
        else if (_executableJobs.TryGetValue(jobId, out var executableJob))
        {
            UntrackExecutionJob(executableJob.ExecutionId, jobId);
        }

        _jobs.TryRemove(jobId, out _);
        _timerJobs.TryRemove(jobId, out _);
        _executableJobs.TryRemove(jobId, out _);
        _deadLetterJobs.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task<int> SetJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        var changed = false;
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Retries = retries;
            changed = true;
        }

        if (_timerJobs.TryGetValue(jobId, out var timerJob))
        {
            timerJob.Retries = retries;
            changed = true;
        }

        if (_executableJobs.TryGetValue(jobId, out var executableJob))
        {
            executableJob.Retries = retries;
            changed = true;
        }

        if (_deadLetterJobs.TryGetValue(jobId, out var deadLetterJob))
        {
            deadLetterJob.Retries = retries;
            changed = true;
        }

        return Task.FromResult(changed ? retries : 0);
    }

    public global::System.Threading.Tasks.Task<TimerJobEntity?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
            return Task.FromResult<TimerJobEntity?>(job);

        if (_timerJobs.TryGetValue(jobId, out var timerJob))
            return Task.FromResult<TimerJobEntity?>(timerJob);

        if (_executableJobs.TryGetValue(jobId, out var executableJob))
            return Task.FromResult<TimerJobEntity?>(ToTimerProjection(executableJob));

        if (_deadLetterJobs.TryGetValue(jobId, out var deadLetterJob))
            return Task.FromResult<TimerJobEntity?>(ToTimerProjection(deadLetterJob));

        return Task.FromResult<TimerJobEntity?>(null);
    }

    public global::System.Threading.Tasks.Task CancelTimerJobAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
    {
        if (_executionJobs.TryGetValue(executionId, out var jobIds))
        {
            foreach (var jobId in jobIds.ToList())
            {
                if (_jobs.TryGetValue(jobId, out var job) &&
                    job.HandlerConfiguration?.Contains(activityId) == true)
                {
                    _jobs.TryRemove(jobId, out _);
                    _timerJobs.TryRemove(jobId, out _);
                    _executableJobs.TryRemove(jobId, out _);
                    jobIds.Remove(jobId);
                }
            }
        }
        return Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task<TimerJobEntity?> GetTimerJobByExecutionAndActivityAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
    {
        if (_executionJobs.TryGetValue(executionId, out var jobIds))
        {
            foreach (var jobId in jobIds)
            {
                if (_jobs.TryGetValue(jobId, out var job) &&
                    job.HandlerConfiguration?.Contains(activityId) == true)
                {
                    return Task.FromResult<TimerJobEntity?>(job);
                }
            }
        }
        return Task.FromResult<TimerJobEntity?>(null);
    }

    public global::System.Threading.Tasks.Task<IReadOnlyList<JobEntity>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = _jobs.Values
            .Select(ToJobProjection)
            .ToList();
        return Task.FromResult<IReadOnlyList<JobEntity>>(jobs);
    }

    public global::System.Threading.Tasks.Task<IReadOnlyList<TimerJobEntity>> GetTimerJobsAsync(CancellationToken cancellationToken = default)
    {
        var timerJobs = _timerJobs.Values.ToList();
        return Task.FromResult<IReadOnlyList<TimerJobEntity>>(timerJobs);
    }

    public global::System.Threading.Tasks.Task<IReadOnlyList<JobEntity>> GetDeadLetterJobsAsync(CancellationToken cancellationToken = default)
    {
        var deadLetterJobs = _deadLetterJobs.Values.ToList();
        return Task.FromResult<IReadOnlyList<JobEntity>>(deadLetterJobs);
    }

    public global::System.Threading.Tasks.Task MoveTimerToExecutableJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return MoveTimerToExecutableJobAndGetAsync(jobId, cancellationToken);
    }

    public global::System.Threading.Tasks.Task<TimerJobEntity> CreateAsyncJobAsync(
        string executionId,
        string processInstanceId,
        string processDefinitionId,
        bool exclusive,
        CancellationToken cancellationToken = default)
    {
        var job = new TimerJobEntity
        {
            Id = Interlocked.Increment(ref _idCounter).ToString(),
            JobType = AbstractJobEntity.JobTypeAsyncContinuation,
            ExecutionId = executionId,
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionId = processDefinitionId,
            HandlerType = "async-continuation",
            HandlerConfiguration = exclusive ? "exclusive" : "async",
            IsExclusive = exclusive
        };
        TrackExecutionJob(executionId, job.Id!);
        return Task.FromResult(job);
    }

    public global::System.Threading.Tasks.Task ScheduleAsyncJobAsync(TimerJobEntity job, CancellationToken cancellationToken = default)
    {
        if (job.Id != null)
        {
            job.JobType = AbstractJobEntity.JobTypeAsyncContinuation;
            _jobs[job.Id] = job;
            _executableJobs[job.Id] = ToExecutableJob(job, AbstractJobEntity.JobTypeAsyncContinuation);
            if (job.ExecutionId != null)
                TrackExecutionJob(job.ExecutionId, job.Id);
        }
        return Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task<IReadOnlyList<JobEntity>> AcquireJobsAsync(
        int maxCount,
        string lockOwner,
        TimeSpan lockTime,
        CancellationToken cancellationToken = default)
    {
        var now = AbpTimeIdProvider.UtcNow;
        var acquired = _executableJobs.Values
            .Where(job => IsExecutable(job, now))
            .OrderBy(job => job.DueDate ?? DateTime.MinValue)
            .ThenBy(job => job.CreatedTime)
            .Take(Math.Max(0, maxCount))
            .ToList();

        foreach (var job in acquired)
        {
            job.State = JobState.Acquired;
            job.LockOwner = lockOwner;
            job.LockExpirationTime = now.Add(lockTime);
            if (_jobs.TryGetValue(job.Id!, out var timerProjection))
                ApplyLock(timerProjection, lockOwner, now.Add(lockTime));
        }

        return Task.FromResult<IReadOnlyList<JobEntity>>(acquired);
    }

    public global::System.Threading.Tasks.Task<IReadOnlyList<TimerJobEntity>> AcquireTimerJobsAsync(
        int maxCount,
        string lockOwner,
        TimeSpan lockTime,
        CancellationToken cancellationToken = default)
    {
        var now = AbpTimeIdProvider.UtcNow;
        var acquired = _timerJobs.Values
            .Where(job => IsTimerDue(job, now))
            .OrderBy(job => job.DueDate ?? DateTime.MinValue)
            .ThenBy(job => job.CreatedTime)
            .Take(Math.Max(0, maxCount))
            .ToList();

        foreach (var job in acquired)
        {
            job.State = JobState.Acquired;
            ApplyLock(job, lockOwner, now.Add(lockTime));
        }

        return Task.FromResult<IReadOnlyList<TimerJobEntity>>(acquired);
    }

    public global::System.Threading.Tasks.Task<IReadOnlyList<JobEntity>> FindExpiredJobsAsync(int pageSize, CancellationToken cancellationToken = default)
    {
        var now = AbpTimeIdProvider.UtcNow;
        var expired = _executableJobs.Values
            .Where(job => job.LockExpirationTime != null && job.LockExpirationTime <= now)
            .OrderBy(job => job.LockExpirationTime)
            .Take(Math.Max(0, pageSize))
            .ToList();

        return Task.FromResult<IReadOnlyList<JobEntity>>(expired);
    }

    public global::System.Threading.Tasks.Task ResetExpiredJobsAsync(IEnumerable<string> jobIds, CancellationToken cancellationToken = default)
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

            if (_jobs.TryGetValue(jobId, out var projection))
            {
                projection.LockOwner = null;
                projection.LockExpirationTime = null;
            }
        }

        return Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task<JobEntity?> MoveTimerToExecutableJobAndGetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (!_timerJobs.TryRemove(jobId, out var timerJob))
            return Task.FromResult<JobEntity?>(null);

        _jobs.TryRemove(jobId, out _);
        var executableJob = ToExecutableJob(timerJob, AbstractJobEntity.JobTypeTimer);
        _executableJobs[executableJob.Id!] = executableJob;
        _jobs[executableJob.Id!] = ToTimerProjection(executableJob);
        return Task.FromResult<JobEntity?>(executableJob);
    }

    public global::System.Threading.Tasks.Task<JobEntity?> MoveJobToDeadLetterAsync(string jobId, string? exceptionMessage, CancellationToken cancellationToken = default)
    {
        if (!_executableJobs.TryRemove(jobId, out var job))
            return Task.FromResult<JobEntity?>(null);

        _jobs.TryRemove(jobId, out _);
        job.State = JobState.DeadLetter;
        job.ExceptionMessage = exceptionMessage;
        job.LockOwner = null;
        job.LockExpirationTime = null;
        _deadLetterJobs[jobId] = job;
        return Task.FromResult<JobEntity?>(job);
    }

    public global::System.Threading.Tasks.Task<JobEntity?> MoveDeadLetterJobToExecutableAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        if (!_deadLetterJobs.TryRemove(jobId, out var job))
            return Task.FromResult<JobEntity?>(null);

        job.State = JobState.Created;
        job.Retries = retries;
        job.ExceptionMessage = null;
        job.LockOwner = null;
        job.LockExpirationTime = null;
        _executableJobs[jobId] = job;
        _jobs[jobId] = ToTimerProjection(job);
        return Task.FromResult<JobEntity?>(job);
    }

    public global::System.Threading.Tasks.Task<bool> LockJobAsync(string jobId, string lockOwner, DateTime lockExpirationTime, CancellationToken cancellationToken = default)
    {
        if (!_executableJobs.TryGetValue(jobId, out var job))
            return Task.FromResult(false);

        var now = AbpTimeIdProvider.UtcNow;
        if (job.LockOwner != null && job.LockExpirationTime > now)
            return Task.FromResult(false);

        job.LockOwner = lockOwner;
        job.LockExpirationTime = lockExpirationTime;
        job.State = JobState.Acquired;
        if (_jobs.TryGetValue(jobId, out var projection))
            ApplyLock(projection, lockOwner, lockExpirationTime);

        return Task.FromResult(true);
    }

    public global::System.Threading.Tasks.Task UnlockJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (_executableJobs.TryGetValue(jobId, out var job))
        {
            job.LockOwner = null;
            job.LockExpirationTime = null;
            if (job.State == JobState.Acquired || job.State == JobState.Executing)
                job.State = JobState.Created;
        }

        if (_jobs.TryGetValue(jobId, out var projection))
        {
            projection.LockOwner = null;
            projection.LockExpirationTime = null;
            if (projection.State == JobState.Acquired || projection.State == JobState.Executing)
                projection.State = JobState.Created;
        }

        return Task.CompletedTask;
    }

    public global::System.Threading.Tasks.Task CancelJobsByExecutionAsync(string executionId, CancellationToken cancellationToken = default)
    {
        if (!_executionJobs.TryRemove(executionId, out var jobIds))
            return Task.CompletedTask;

        foreach (var jobId in jobIds.ToList())
        {
            _jobs.TryRemove(jobId, out _);
            _timerJobs.TryRemove(jobId, out _);
            _executableJobs.TryRemove(jobId, out _);
            _deadLetterJobs.TryRemove(jobId, out _);
        }

        return Task.CompletedTask;
    }

    private static bool IsExecutable(JobEntity job, DateTime now)
    {
        return job.State == JobState.Created
            && job.Retries > 0
            && (job.DueDate == null || job.DueDate <= now)
            && (job.LockOwner == null || job.LockExpirationTime <= now);
    }

    private static bool IsTimerDue(TimerJobEntity job, DateTime now)
    {
        return job.State == JobState.Created
            && job.Retries > 0
            && (job.DueDate == null || job.DueDate <= now)
            && (job.LockOwner == null || job.LockExpirationTime <= now);
    }

    private static void ApplyLock(TimerJobEntity job, string lockOwner, DateTime lockExpirationTime)
    {
        job.State = JobState.Acquired;
        job.LockOwner = lockOwner;
        job.LockExpirationTime = lockExpirationTime;
    }

    private static JobEntity ToExecutableJob(TimerJobEntity job, string jobType)
    {
        return new JobEntity
        {
            Id = job.Id ?? AbpTimeIdProvider.NewGuid("N"),
            JobType = jobType,
            ExecutionId = job.ExecutionId,
            ProcessInstanceId = job.ProcessInstanceId,
            ProcessDefinitionId = job.ProcessDefinitionId,
            HandlerType = job.HandlerType,
            HandlerConfiguration = job.HandlerConfiguration,
            Retries = job.Retries,
            ExceptionMessage = job.ExceptionMessage,
            ExceptionStackId = job.ExceptionStackId,
            DueDate = job.DueDate,
            Repeat = job.Repeat,
            IsExclusive = job.IsExclusive || string.Equals(job.HandlerConfiguration, "exclusive", StringComparison.OrdinalIgnoreCase),
            TenantId = job.TenantId,
            CreatedTime = job.CreatedTime,
            LockOwner = job.LockOwner,
            LockExpirationTime = job.LockExpirationTime,
            State = job.State
        };
    }

    private static TimerJobEntity ToTimerProjection(JobEntity job)
    {
        return new TimerJobEntity
        {
            Id = job.Id,
            JobType = job.JobType,
            ExecutionId = job.ExecutionId,
            ProcessInstanceId = job.ProcessInstanceId,
            ProcessDefinitionId = job.ProcessDefinitionId,
            DueDate = job.DueDate,
            Repeat = job.Repeat,
            HandlerType = job.HandlerType,
            HandlerConfiguration = job.HandlerConfiguration,
            Retries = job.Retries,
            ExceptionMessage = job.ExceptionMessage,
            ExceptionStackId = job.ExceptionStackId,
            TenantId = job.TenantId,
            CreatedTime = job.CreatedTime,
            LockOwner = job.LockOwner,
            LockExpirationTime = job.LockExpirationTime,
            IsExclusive = job.IsExclusive,
            State = job.State
        };
    }

    private static JobEntity ToJobProjection(TimerJobEntity job)
    {
        return new JobEntity
        {
            Id = job.Id,
            JobType = job.JobType,
            ExecutionId = job.ExecutionId,
            ProcessInstanceId = job.ProcessInstanceId,
            ProcessDefinitionId = job.ProcessDefinitionId,
            DueDate = job.DueDate,
            Repeat = job.Repeat,
            HandlerType = job.HandlerType,
            HandlerConfiguration = job.HandlerConfiguration,
            Retries = job.Retries,
            ExceptionMessage = job.ExceptionMessage,
            ExceptionStackId = job.ExceptionStackId,
            TenantId = job.TenantId,
            CreatedTime = job.CreatedTime,
            LockOwner = job.LockOwner,
            LockExpirationTime = job.LockExpirationTime,
            IsExclusive = job.IsExclusive,
            State = job.State
        };
    }

    private void TrackExecutionJob(string executionId, string jobId)
    {
        _executionJobs.AddOrUpdate(executionId,
            _ => new List<string> { jobId },
            (_, list) =>
            {
                lock (list)
                {
                    if (!list.Contains(jobId))
                        list.Add(jobId);
                }

                return list;
            });
    }

    private void UntrackExecutionJob(string? executionId, string jobId)
    {
        if (executionId == null || !_executionJobs.TryGetValue(executionId, out var list))
            return;

        lock (list)
        {
            list.Remove(jobId);
            if (list.Count == 0)
                _executionJobs.TryRemove(executionId, out _);
        }
    }
}

