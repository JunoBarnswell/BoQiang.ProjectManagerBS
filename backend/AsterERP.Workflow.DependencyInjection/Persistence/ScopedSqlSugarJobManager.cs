using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Job;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using CoreJobEntity = AsterERP.Workflow.Core.Job.JobEntity;
using CoreTimerJobEntity = AsterERP.Workflow.Core.Job.TimerJobEntity;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

public sealed class ScopedSqlSugarJobManager : IJobManager, IJobLifecycleManager
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ScopedSqlSugarJobManager(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<CoreTimerJobEntity> CreateTimerJobAsync(string executionId, string processInstanceId, string processDefinitionId, DateTime? dueDate, string? repeat, string handlerType, string? handlerConfiguration, string? tenantId = null, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.CreateTimerJobAsync(executionId, processInstanceId, processDefinitionId, dueDate, repeat, handlerType, handlerConfiguration, tenantId, cancellationToken));

    public Task ScheduleTimerJobAsync(CoreTimerJobEntity job, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.ScheduleTimerJobAsync(job, cancellationToken));

    public Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.ExecuteJobAsync(jobId, cancellationToken));

    public Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.DeleteJobAsync(jobId, cancellationToken));

    public Task<int> SetJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.SetJobRetriesAsync(jobId, retries, cancellationToken));

    public Task<CoreTimerJobEntity?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.GetJobAsync(jobId, cancellationToken));

    public Task CancelTimerJobAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.CancelTimerJobAsync(executionId, activityId, cancellationToken));

    public Task<CoreTimerJobEntity?> GetTimerJobByExecutionAndActivityAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.GetTimerJobByExecutionAndActivityAsync(executionId, activityId, cancellationToken));

    public Task MoveTimerToExecutableJobAsync(string jobId, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.MoveTimerToExecutableJobAsync(jobId, cancellationToken));

    public Task<CoreTimerJobEntity> CreateAsyncJobAsync(string executionId, string processInstanceId, string processDefinitionId, bool exclusive, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.CreateAsyncJobAsync(executionId, processInstanceId, processDefinitionId, exclusive, cancellationToken));

    public Task ScheduleAsyncJobAsync(CoreTimerJobEntity job, CancellationToken cancellationToken = default)
        => WithManagerAsync(manager => manager.ScheduleAsyncJobAsync(job, cancellationToken));

    public Task<IReadOnlyList<CoreJobEntity>> AcquireJobsAsync(int maxCount, string lockOwner, TimeSpan lockTime, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.AcquireJobsAsync(maxCount, lockOwner, lockTime, cancellationToken));

    public Task<IReadOnlyList<CoreTimerJobEntity>> AcquireTimerJobsAsync(int maxCount, string lockOwner, TimeSpan lockTime, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.AcquireTimerJobsAsync(maxCount, lockOwner, lockTime, cancellationToken));

    public Task<IReadOnlyList<CoreJobEntity>> FindExpiredJobsAsync(int pageSize, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.FindExpiredJobsAsync(pageSize, cancellationToken));

    public Task ResetExpiredJobsAsync(IEnumerable<string> jobIds, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.ResetExpiredJobsAsync(jobIds, cancellationToken));

    public Task<CoreJobEntity?> MoveTimerToExecutableJobAndGetAsync(string jobId, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.MoveTimerToExecutableJobAndGetAsync(jobId, cancellationToken));

    public Task<CoreJobEntity?> MoveJobToDeadLetterAsync(string jobId, string? exceptionMessage, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.MoveJobToDeadLetterAsync(jobId, exceptionMessage, cancellationToken));

    public Task<CoreJobEntity?> MoveDeadLetterJobToExecutableAsync(string jobId, int retries, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.MoveDeadLetterJobToExecutableAsync(jobId, retries, cancellationToken));

    public Task<bool> LockJobAsync(string jobId, string lockOwner, DateTime lockExpirationTime, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.LockJobAsync(jobId, lockOwner, lockExpirationTime, cancellationToken));

    public Task UnlockJobAsync(string jobId, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.UnlockJobAsync(jobId, cancellationToken));

    public Task CancelJobsByExecutionAsync(string executionId, CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.CancelJobsByExecutionAsync(executionId, cancellationToken));

    public Task<IReadOnlyList<CoreJobEntity>> GetJobsAsync(CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.GetJobsAsync(cancellationToken));

    public Task<IReadOnlyList<CoreTimerJobEntity>> GetTimerJobsAsync(CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.GetTimerJobsAsync(cancellationToken));

    public Task<IReadOnlyList<CoreJobEntity>> GetDeadLetterJobsAsync(CancellationToken cancellationToken = default)
        => WithLifecycleManagerAsync(manager => manager.GetDeadLetterJobsAsync(cancellationToken));

    private Task<TResult> WithLifecycleManagerAsync<TResult>(Func<IJobLifecycleManager, Task<TResult>> action)
    {
        var currentProvider = ProcessEngineServiceProviderAccessor.Current;
        if (currentProvider != null)
        {
            var db = currentProvider.GetService<ISqlSugarClient>();
            if (db != null)
            {
                return action(new SqlSugarJobManager(db));
            }
        }

        return WithScopedLifecycleManagerAsync(action);
    }

    private Task WithLifecycleManagerAsync(Func<IJobLifecycleManager, Task> action)
    {
        var currentProvider = ProcessEngineServiceProviderAccessor.Current;
        if (currentProvider != null)
        {
            var db = currentProvider.GetService<ISqlSugarClient>();
            if (db != null)
            {
                return action(new SqlSugarJobManager(db));
            }
        }

        return WithScopedLifecycleManagerAsync(action);
    }

    private async Task<TResult> WithScopedLifecycleManagerAsync<TResult>(Func<IJobLifecycleManager, Task<TResult>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var scopedDb = GetRequiredSqlSugarClient(scope.ServiceProvider);
        return await action(new SqlSugarJobManager(scopedDb));
    }

    private async Task WithScopedLifecycleManagerAsync(Func<IJobLifecycleManager, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var scopedDb = GetRequiredSqlSugarClient(scope.ServiceProvider);
        await action(new SqlSugarJobManager(scopedDb));
    }

    private async Task<TResult> WithManagerAsync<TResult>(Func<IJobManager, Task<TResult>> action)
    {
        var currentProvider = ProcessEngineServiceProviderAccessor.Current;
        if (currentProvider != null)
        {
            var db = currentProvider.GetService<ISqlSugarClient>();
            if (db != null)
            {
                return await action(new SqlSugarJobManager(db));
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var scopedDb = GetRequiredSqlSugarClient(scope.ServiceProvider);
        return await action(new SqlSugarJobManager(scopedDb));
    }

    private async Task WithManagerAsync(Func<IJobManager, Task> action)
    {
        var currentProvider = ProcessEngineServiceProviderAccessor.Current;
        if (currentProvider != null)
        {
            var db = currentProvider.GetService<ISqlSugarClient>();
            if (db != null)
            {
                await action(new SqlSugarJobManager(db));
                return;
            }
        }

        using var scope = _scopeFactory.CreateScope();
        var scopedDb = GetRequiredSqlSugarClient(scope.ServiceProvider);
        await action(new SqlSugarJobManager(scopedDb));
    }

    private static ISqlSugarClient GetRequiredSqlSugarClient(IServiceProvider serviceProvider) =>
        serviceProvider.GetService<ISqlSugarClient>()
        ?? throw new InvalidOperationException("Workflow job persistence requires an ISqlSugarClient registration.");
}
