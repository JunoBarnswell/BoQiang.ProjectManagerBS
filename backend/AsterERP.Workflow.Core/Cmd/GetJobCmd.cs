using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class GetJobCmd : ICommand<JobRecord?>
{
    private readonly string _jobId;
    private readonly CmdJobType _jobType;

    public GetJobCmd(string jobId, CmdJobType jobType)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new WorkflowEngineArgumentException("jobId is null");
        if (!Enum.IsDefined(typeof(CmdJobType), jobType))
            throw new WorkflowEngineArgumentException($"Unsupported job type enum value '{jobType}'.");

        _jobId = jobId;
        _jobType = jobType;
    }


    public async Task<JobRecord?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            throw new WorkflowEngineException("Process engine job manager is not configured.");

        var job = await ResolveJobByTypeAsync(jobManager, cancellationToken);
        if (job == null)
            throw new WorkflowEngineObjectNotFoundException($"Job '{_jobId}' not found for type '{ToJobTypeString(_jobType)}'.");

        return ToJobRecord(job, ToJobTypeString(_jobType));
    }

    private async Task<TimerJobEntity?> ResolveJobByTypeAsync(IJobManager jobManager, CancellationToken cancellationToken)
    {
        if (jobManager is not IJobLifecycleManager lifecycleManager)
        {
            throw new WorkflowEngineException(
                "Current job manager does not support lifecycle operations for typed job lookup.");
        }

        return _jobType switch
        {
            CmdJobType.Timer => await FindTimerJobAsync(lifecycleManager, cancellationToken),
            CmdJobType.DeadLetter => await FindDeadLetterJobAsync(lifecycleManager, cancellationToken),
            CmdJobType.AsyncContinuation => await FindExecutableJobAsync(lifecycleManager, AbstractJobEntity.JobTypeAsyncContinuation, cancellationToken),
            CmdJobType.Message => await FindExecutableJobAsync(lifecycleManager, AbstractJobEntity.JobTypeMessage, cancellationToken),
            _ => throw new WorkflowEngineArgumentException($"Unsupported job type enum value '{_jobType}'.")
        };
    }

    private async Task<TimerJobEntity?> FindTimerJobAsync(IJobLifecycleManager lifecycleManager, CancellationToken cancellationToken)
    {
        var timerJobs = await lifecycleManager.GetTimerJobsAsync(cancellationToken);
        return timerJobs.FirstOrDefault(job => string.Equals(job.Id, _jobId, StringComparison.Ordinal));
    }

    private async Task<TimerJobEntity?> FindDeadLetterJobAsync(IJobLifecycleManager lifecycleManager, CancellationToken cancellationToken)
    {
        var deadLetterJobs = await lifecycleManager.GetDeadLetterJobsAsync(cancellationToken);
        var deadLetterJob = deadLetterJobs.FirstOrDefault(job => string.Equals(job.Id, _jobId, StringComparison.Ordinal));
        return deadLetterJob == null ? null : ToTimerProjection(deadLetterJob);
    }

    private async Task<TimerJobEntity?> FindExecutableJobAsync(IJobLifecycleManager lifecycleManager, string expectedType, CancellationToken cancellationToken)
    {
        var executableJobs = await lifecycleManager.GetJobsAsync(cancellationToken);
        var executableJob = executableJobs.FirstOrDefault(job =>
            string.Equals(job.Id, _jobId, StringComparison.Ordinal) &&
            job.JobType == expectedType);
        return executableJob == null ? null : ToTimerProjection(executableJob);
    }

    private bool MatchesRequestedType(TimerJobEntity job)
    {
        return _jobType switch
        {
            CmdJobType.Timer => job.JobType == AbstractJobEntity.JobTypeTimer,
            CmdJobType.Message => job.JobType == AbstractJobEntity.JobTypeMessage,
            CmdJobType.AsyncContinuation => job.JobType == AbstractJobEntity.JobTypeAsyncContinuation,
            CmdJobType.DeadLetter => job.JobType == AbstractJobEntity.JobTypeDeadLetter,
            _ => false
        };
    }

    private static string ToJobTypeString(CmdJobType jobType)
    {
        return jobType switch
        {
            CmdJobType.Timer => "timer",
            CmdJobType.Message => "message",
            CmdJobType.AsyncContinuation => "async",
            CmdJobType.DeadLetter => "deadletter",
            _ => throw new WorkflowEngineArgumentException($"Unsupported job type enum value '{jobType}'.")
        };
    }

    private static JobRecord ToJobRecord(TimerJobEntity job, string jobType)
    {
        return new JobRecord
        {
            Id = job.Id ?? string.Empty,
            Type = jobType,
            Retries = job.Retries,
            ExecutionId = job.ExecutionId,
            ProcessInstanceId = job.ProcessInstanceId,
            ProcessDefinitionId = job.ProcessDefinitionId,
            ExceptionMessage = job.ExceptionMessage,
            DueDate = job.DueDate,
            HandlerType = job.HandlerType,
            TenantId = job.TenantId
        };
    }

    private static TimerJobEntity ToTimerProjection(JobEntity job)
    {
        return new TimerJobEntity
        {
            Id = job.Id,
            JobType = job.JobType,
            State = job.State,
            ExecutionId = job.ExecutionId,
            ProcessInstanceId = job.ProcessInstanceId,
            ProcessDefinitionId = job.ProcessDefinitionId,
            DueDate = job.DueDate,
            Repeat = job.Repeat,
            HandlerType = job.HandlerType,
            HandlerConfiguration = job.HandlerConfiguration,
            Retries = job.Retries,
            ExceptionMessage = job.ExceptionMessage,
            TenantId = job.TenantId,
            CreatedTime = job.CreatedTime,
            LockOwner = job.LockOwner,
            LockExpirationTime = job.LockExpirationTime,
            IsExclusive = job.IsExclusive
        };
    }
}

