using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Job;

namespace AsterERP.Api.Tests;

internal sealed class RecordingJobManager : IJobManager
{
    private readonly Exception _exception;

    public RecordingJobManager(Exception exception)
    {
        _exception = exception;
    }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<TimerJobEntity> CreateTimerJobAsync(
        string executionId,
        string processInstanceId,
        string processDefinitionId,
        DateTime? dueDate,
        string? repeat,
        string handlerType,
        string? handlerConfiguration,
        string? tenantId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new TimerJobEntity());

    public Task ScheduleTimerJobAsync(TimerJobEntity job, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ExecuteJobAsync(string jobId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<int> SetJobRetriesAsync(string jobId, int retries, CancellationToken cancellationToken = default) =>
        Task.FromResult(retries);

    public Task<TimerJobEntity?> GetJobAsync(string jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult<TimerJobEntity?>(null);

    public Task CancelTimerJobAsync(string executionId, string activityId, CancellationToken cancellationToken = default)
    {
        ReceivedCancellationToken = cancellationToken;
        return Task.FromException(_exception);
    }

    public Task<TimerJobEntity?> GetTimerJobByExecutionAndActivityAsync(
        string executionId,
        string activityId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<TimerJobEntity?>(null);

    public Task MoveTimerToExecutableJobAsync(string jobId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<TimerJobEntity> CreateAsyncJobAsync(
        string executionId,
        string processInstanceId,
        string processDefinitionId,
        bool exclusive,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new TimerJobEntity());

    public Task ScheduleAsyncJobAsync(TimerJobEntity job, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
