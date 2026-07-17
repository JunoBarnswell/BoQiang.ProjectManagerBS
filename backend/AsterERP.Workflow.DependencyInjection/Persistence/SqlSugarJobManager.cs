using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Persistence;
using SqlSugar;
using CoreJobEntity = AsterERP.Workflow.Core.Job.JobEntity;
using CoreTimerJobEntity = AsterERP.Workflow.Core.Job.TimerJobEntity;
using PersistentJobEntity = AsterERP.Workflow.Persistence.Entities.JobEntity;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

public sealed class SqlSugarJobManager : IJobManager, IJobLifecycleManager
{
    private readonly ISqlSugarClient _db;

    public SqlSugarJobManager(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<CoreTimerJobEntity> CreateTimerJobAsync(
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
        await Task.CompletedTask;
        return new CoreTimerJobEntity
        {
            Id = AbpTimeIdProvider.NewGuid("N"),
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
    }

    public Task ScheduleTimerJobAsync(CoreTimerJobEntity job, CancellationToken cancellationToken = default)
    {
        job.JobType = AbstractJobEntity.JobTypeTimer;
        return InsertAsync(job, AbstractJobEntity.JobTypeTimer, cancellationToken);
    }

    public Task<CoreTimerJobEntity> CreateAsyncJobAsync(
        string executionId,
        string processInstanceId,
        string processDefinitionId,
        bool exclusive,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CoreTimerJobEntity
        {
            Id = AbpTimeIdProvider.NewGuid("N"),
            JobType = AbstractJobEntity.JobTypeAsyncContinuation,
            ExecutionId = executionId,
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionId = processDefinitionId,
            HandlerType = AsyncContinuationJobHandler.HandlerType,
            HandlerConfiguration = exclusive ? "exclusive" : "async",
            IsExclusive = exclusive
        });
    }

    public Task ScheduleAsyncJobAsync(CoreTimerJobEntity job, CancellationToken cancellationToken = default)
    {
        job.JobType = AbstractJobEntity.JobTypeAsyncContinuation;
        if (string.IsNullOrWhiteSpace(job.HandlerType))
        {
            job.HandlerType = AsyncContinuationJobHandler.HandlerType;
        }

        return InsertAsync(job, AbstractJobEntity.JobTypeAsyncContinuation, cancellationToken);
    }

    public Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return DeleteJobsByIdsAsync([jobId], cancellationToken);
    }

    public Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return DeleteJobsByIdsAsync([jobId], cancellationToken);
    }

    public async Task<int> SetJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        var row = await GetJobRowByIdAsync(jobId);
        if (row == null)
        {
            return 0;
        }

        await ApplyOptimisticJobUpdateAsync(
            row,
            new PersistentJobEntity
            {
                Retries = retries
            },
            persisted =>
            {
                persisted.Retries = retries;
            },
            cancellationToken);
        return retries;
    }

    public async Task<CoreTimerJobEntity?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await LoadJobAsync(jobId, ToCoreTimerJob);
    }

    public async Task CancelTimerJobAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
    {
        var ids = (await BuildTimerJobByExecutionAndActivityQuery(executionId, activityId)
            .Select(it => it.Id)
            .ToListAsync(cancellationToken));
        if (ids.Count > 0)
        {
            await DeleteJobsByIdsAsync(ids, cancellationToken);
        }
    }

    public async Task<CoreTimerJobEntity?> GetTimerJobByExecutionAndActivityAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
    {
        var match = await BuildTimerJobByExecutionAndActivityQuery(executionId, activityId)
            .FirstAsync(cancellationToken);
        return match == null ? null : ToCoreTimerJob(match);
    }

    public Task MoveTimerToExecutableJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return MoveTimerToExecutableJobAndGetAsync(jobId, cancellationToken);
    }

    public async Task<IReadOnlyList<CoreJobEntity>> AcquireJobsAsync(int maxCount, string lockOwner, TimeSpan lockTime, CancellationToken cancellationToken = default)
    {
        return await AcquireByTypeAsync(
            AbstractJobEntity.JobTypeAsyncContinuation,
            maxCount,
            lockOwner,
            lockTime,
            ToCoreJob,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoreTimerJobEntity>> AcquireTimerJobsAsync(int maxCount, string lockOwner, TimeSpan lockTime, CancellationToken cancellationToken = default)
    {
        return await AcquireByTypeAsync(
            AbstractJobEntity.JobTypeTimer,
            maxCount,
            lockOwner,
            lockTime,
            ToCoreTimerJob,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoreJobEntity>> FindExpiredJobsAsync(int pageSize, CancellationToken cancellationToken = default)
    {
        var now = AbpTimeIdProvider.UtcNow;
        return await QueryJobsAsync(
            query => query
                .Where(it => it.LockExpirationTime != null && it.LockExpirationTime <= now)
                .OrderBy(it => it.LockExpirationTime, OrderByType.Asc)
                .Take(Math.Max(0, pageSize)),
            ToCoreJob,
            cancellationToken);
    }

    public async Task ResetExpiredJobsAsync(IEnumerable<string> jobIds, CancellationToken cancellationToken = default)
    {
        var ids = jobIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        await ResetJobsToCreatedStateAsync(ids, cancellationToken);
    }

    public async Task<CoreJobEntity?> MoveTimerToExecutableJobAndGetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return await LoadJobAsync(jobId, ToCoreJob);
    }

    public async Task<CoreJobEntity?> MoveJobToDeadLetterAsync(string jobId, string? exceptionMessage, CancellationToken cancellationToken = default)
    {
        var row = await GetJobRowByIdAsync(jobId);
        if (row == null)
        {
            return null;
        }

        await ApplyOptimisticJobUpdateAsync(
            row,
            new PersistentJobEntity
            {
                State = (int)JobState.DeadLetter,
                ExceptionMessage = exceptionMessage,
                LockOwner = null,
                LockExpirationTime = null
            },
            persisted =>
            {
                persisted.State = (int)JobState.DeadLetter;
                persisted.ExceptionMessage = exceptionMessage;
                persisted.LockOwner = null;
                persisted.LockExpirationTime = null;
            },
            cancellationToken);
        return ToCoreJob(row);
    }

    public async Task<CoreJobEntity?> MoveDeadLetterJobToExecutableAsync(string jobId, int retries, CancellationToken cancellationToken = default)
    {
        var row = await GetJobRowByIdAsync(jobId);
        if (row == null || row.State != (int)JobState.DeadLetter)
        {
            return null;
        }

        await ApplyOptimisticJobUpdateAsync(
            row,
            new PersistentJobEntity
            {
                State = (int)JobState.Created,
                Retries = retries,
                ExceptionMessage = null,
                LockOwner = null,
                LockExpirationTime = null
            },
            persisted =>
            {
                persisted.State = (int)JobState.Created;
                persisted.Retries = retries;
                persisted.ExceptionMessage = null;
                persisted.LockOwner = null;
                persisted.LockExpirationTime = null;
            },
            cancellationToken);
        return ToCoreJob(row);
    }

    public async Task<bool> LockJobAsync(string jobId, string lockOwner, DateTime lockExpirationTime, CancellationToken cancellationToken = default)
    {
        var now = AbpTimeIdProvider.UtcNow;
        var affected = await _db.Updateable<PersistentJobEntity>()
            .SetColumns(it => new PersistentJobEntity { LockOwner = lockOwner, LockExpirationTime = lockExpirationTime, State = (int)JobState.Acquired })
            .Where(it => it.Id == jobId && (it.LockOwner == null || it.LockExpirationTime == null || it.LockExpirationTime <= now))
            .ExecuteCommandAsync(cancellationToken);
        return affected > 0;
    }

    public Task UnlockJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        return ResetJobToCreatedStateAsync(jobId, cancellationToken);
    }

    public Task CancelJobsByExecutionAsync(string executionId, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<PersistentJobEntity>().Where(it => it.ExecutionId == executionId).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CoreJobEntity>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        return await QueryJobsAsync(
            query => query.Where(it => it.JobType == AbstractJobEntity.JobTypeAsyncContinuation && it.State != (int)JobState.DeadLetter),
            ToCoreJob,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoreTimerJobEntity>> GetTimerJobsAsync(CancellationToken cancellationToken = default)
    {
        return await QueryJobsAsync(
            query => query.Where(it => it.JobType == AbstractJobEntity.JobTypeTimer && it.State != (int)JobState.DeadLetter),
            ToCoreTimerJob,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CoreJobEntity>> GetDeadLetterJobsAsync(CancellationToken cancellationToken = default)
    {
        return await QueryJobsAsync(
            query => query.Where(it => it.State == (int)JobState.DeadLetter),
            ToCoreJob,
            cancellationToken);
    }

    private async Task<List<PersistentJobEntity>> FindDueAsync(string jobType, int maxCount, CancellationToken cancellationToken)
    {
        var now = AbpTimeIdProvider.UtcNow;
        return await _db.Queryable<PersistentJobEntity>()
            .Where(it => it.JobType == jobType
                && it.State == (int)JobState.Created
                && it.Retries > 0
                && (it.DueDate == null || it.DueDate <= now)
                && (it.LockOwner == null || it.LockExpirationTime == null || it.LockExpirationTime <= now))
            .OrderBy(it => it.DueDate, OrderByType.Asc)
            .Take(Math.Max(0, maxCount))
            .ToListAsync(cancellationToken);
    }

    private Task<PersistentJobEntity?> GetJobRowByIdAsync(string jobId)
    {
        return _db.Queryable<PersistentJobEntity>().InSingleAsync(jobId);
    }

    private Task DeleteJobsByIdsAsync(
        IReadOnlyCollection<string> jobIds,
        CancellationToken cancellationToken)
    {
        return _db.Deleteable<PersistentJobEntity>()
            .In(jobIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList())
            .ExecuteCommandAsync(cancellationToken);
    }

    private ISugarQueryable<PersistentJobEntity> BuildTimerJobByExecutionAndActivityQuery(string executionId, string activityId)
    {
        return _db.Queryable<PersistentJobEntity>()
            .Where(it => it.ExecutionId == executionId
                && it.JobType == AbstractJobEntity.JobTypeTimer
                && it.HandlerConfiguration != null
                && it.HandlerConfiguration.Contains(activityId));
    }

    private async Task<TJob?> LoadJobAsync<TJob>(string jobId, Func<PersistentJobEntity, TJob> map)
    {
        var row = await GetJobRowByIdAsync(jobId);
        return row == null ? default : map(row);
    }

    private async Task<IReadOnlyList<TJob>> AcquireByTypeAsync<TJob>(
        string jobType,
        int maxCount,
        string lockOwner,
        TimeSpan lockTime,
        Func<PersistentJobEntity, TJob> map,
        CancellationToken cancellationToken)
    {
        var rows = await FindDueAsync(jobType, maxCount, cancellationToken);
        var acquired = await LockRowsAsync(rows, lockOwner, lockTime, cancellationToken);
        return acquired.Select(map).ToList();
    }

    private async Task<IReadOnlyList<TJob>> QueryJobsAsync<TJob>(
        Func<ISugarQueryable<PersistentJobEntity>, ISugarQueryable<PersistentJobEntity>> buildQuery,
        Func<PersistentJobEntity, TJob> map,
        CancellationToken cancellationToken)
    {
        var rows = await buildQuery(_db.Queryable<PersistentJobEntity>())
            .ToListAsync(cancellationToken);
        return rows.Select(map).ToList();
    }

    private Task ResetJobToCreatedStateAsync(string jobId, CancellationToken cancellationToken)
    {
        return _db.Updateable<PersistentJobEntity>()
            .SetColumns(it => new PersistentJobEntity
            {
                LockOwner = null,
                LockExpirationTime = null,
                State = (int)JobState.Created
            })
            .Where(it => it.Id == jobId)
            .ExecuteCommandAsync(cancellationToken);
    }

    private Task ResetJobsToCreatedStateAsync(
        IReadOnlyCollection<string> jobIds,
        CancellationToken cancellationToken)
    {
        return _db.Updateable<PersistentJobEntity>()
            .SetColumns(it => new PersistentJobEntity
            {
                LockOwner = null,
                LockExpirationTime = null,
                State = (int)JobState.Created
            })
            .Where(it => jobIds.Contains(it.Id))
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task ApplyOptimisticJobUpdateAsync(
        PersistentJobEntity row,
        PersistentJobEntity update,
        Action<PersistentJobEntity> applyToLoadedRow,
        CancellationToken cancellationToken)
    {
        update.Revision = row.Revision + 1;

        var affected = await _db.Updateable<PersistentJobEntity>()
            .SetColumns(_ => update)
            .Where(it => it.Id == row.Id && it.Revision == row.Revision)
            .ExecuteCommandAsync(cancellationToken);
        if (affected == 0)
        {
            throw new WorkflowEngineOptimisticLockingException(
                $"Optimistic locking failed for job '{row.Id}' at revision {row.Revision}.");
        }

        applyToLoadedRow(row);
        row.Revision = update.Revision;
    }

    private async Task<List<PersistentJobEntity>> LockRowsAsync(
        List<PersistentJobEntity> rows,
        string lockOwner,
        TimeSpan lockTime,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return new List<PersistentJobEntity>();
        }

        var now = AbpTimeIdProvider.UtcNow;
        var lockExpiration = now.Add(lockTime);
        var ids = rows
            .Select(row => row.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
        {
            return new List<PersistentJobEntity>();
        }

        await _db.Updateable<PersistentJobEntity>()
            .SetColumns(it => new PersistentJobEntity
            {
                LockOwner = lockOwner,
                LockExpirationTime = lockExpiration,
                State = (int)JobState.Acquired
            })
            .Where(it => ids.Contains(it.Id)
                && (it.LockOwner == null || it.LockExpirationTime == null || it.LockExpirationTime <= now))
            .ExecuteCommandAsync(cancellationToken);

        return await _db.Queryable<PersistentJobEntity>()
            .Where(it => ids.Contains(it.Id)
                && it.LockOwner == lockOwner
                && it.State == (int)JobState.Acquired
                && it.LockExpirationTime != null
                && it.LockExpirationTime > now)
            .ToListAsync(cancellationToken);
    }

    private Task InsertAsync(CoreTimerJobEntity job, string jobType, CancellationToken cancellationToken)
    {
        var row = new PersistentJobEntity
        {
            Id = job.Id ?? AbpTimeIdProvider.NewGuid("N"),
            Revision = 1,
            JobType = jobType,
            DueDate = job.DueDate,
            Retries = job.Retries,
            IsExclusive = job.IsExclusive,
            ExecutionId = job.ExecutionId,
            ProcessInstanceId = job.ProcessInstanceId,
            ProcessDefinitionId = job.ProcessDefinitionId,
            Repeat = job.Repeat,
            HandlerType = job.HandlerType,
            HandlerConfiguration = job.HandlerConfiguration,
            ExceptionStackId = job.ExceptionStackId,
            ExceptionMessage = job.ExceptionMessage,
            TenantId = job.TenantId,
            MaxIterations = job.MaxIterations,
            EndDate = job.EndDate,
            LockOwner = job.LockOwner,
            LockExpirationTime = job.LockExpirationTime,
            State = (int)JobState.Created,
            CreatedTime = job.CreatedTime
        };
        job.Id = row.Id;
        var insertable = _db.Insertable(row);
        if (row.LockExpirationTime is null)
        {
            insertable = insertable.IgnoreColumns(it => it.LockExpirationTime);
        }

        if (string.IsNullOrWhiteSpace(row.LockOwner))
        {
            insertable = insertable.IgnoreColumns(it => it.LockOwner);
        }

        return insertable.ExecuteCommandAsync(cancellationToken);
    }

    private static CoreJobEntity ToCoreJob(PersistentJobEntity row)
    {
        return new CoreJobEntity
        {
            Id = row.Id,
            JobType = row.JobType,
            DueDate = row.DueDate,
            Retries = row.Retries,
            IsExclusive = row.IsExclusive,
            ExecutionId = row.ExecutionId,
            ProcessInstanceId = row.ProcessInstanceId,
            ProcessDefinitionId = row.ProcessDefinitionId,
            Repeat = row.Repeat,
            HandlerType = row.HandlerType,
            HandlerConfiguration = row.HandlerConfiguration,
            ExceptionStackId = row.ExceptionStackId,
            ExceptionMessage = row.ExceptionMessage,
            TenantId = row.TenantId,
            MaxIterations = row.MaxIterations,
            EndDate = row.EndDate,
            LockOwner = row.LockOwner,
            LockExpirationTime = row.LockExpirationTime,
            State = (JobState)row.State,
            CreatedTime = row.CreatedTime ?? AbpTimeIdProvider.UtcNow
        };
    }

    private static CoreTimerJobEntity ToCoreTimerJob(PersistentJobEntity row)
    {
        return new CoreTimerJobEntity
        {
            Id = row.Id,
            JobType = row.JobType,
            DueDate = row.DueDate,
            Retries = row.Retries,
            IsExclusive = row.IsExclusive,
            ExecutionId = row.ExecutionId,
            ProcessInstanceId = row.ProcessInstanceId,
            ProcessDefinitionId = row.ProcessDefinitionId,
            Repeat = row.Repeat,
            HandlerType = row.HandlerType,
            HandlerConfiguration = row.HandlerConfiguration,
            ExceptionStackId = row.ExceptionStackId,
            ExceptionMessage = row.ExceptionMessage,
            TenantId = row.TenantId,
            MaxIterations = row.MaxIterations,
            EndDate = row.EndDate,
            LockOwner = row.LockOwner,
            LockExpirationTime = row.LockExpirationTime,
            State = (JobState)row.State,
            CreatedTime = row.CreatedTime ?? AbpTimeIdProvider.UtcNow
        };
    }
}
