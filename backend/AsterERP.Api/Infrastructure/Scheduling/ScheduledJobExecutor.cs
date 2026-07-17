using System.Diagnostics;
using AsterERP.Contracts.System.ScheduledJobs;
using AsterERP.Api.Domain.System.ScheduledJobs;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.System.ScheduledJobs;
using Hangfire;
using Hangfire.Server;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ScheduledJobExecutor(
    IRepository<SystemScheduledJobEntity> scheduledJobRepository,
    IRepository<SystemScheduledJobLogEntity> scheduledJobLogRepository,
    PresetScheduledJobRunner presetRunner,
    IScheduledJobHttpCallbackClient httpCallbackClient,
    ILogger<ScheduledJobExecutor> logger)
{
    [Queue("scheduled-jobs")]
    public async Task ExecuteAsync(string scheduledJobId, string triggerType, PerformContext? context)
    {
        var startedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var traceId = Guid.NewGuid().ToString("N");
        var hangfireJobId = context?.BackgroundJob?.Id;
        var job = await scheduledJobRepository.FirstOrDefaultAsync(item => item.Id == scheduledJobId && !item.IsDeleted);

        if (job is null)
        {
            logger.LogWarning("Scheduled job {ScheduledJobId} was not found", scheduledJobId);
            return;
        }

        var log = new SystemScheduledJobLogEntity
        {
            ScheduledJobId = scheduledJobId,
            HangfireJobId = hangfireJobId,
            TriggerType = triggerType,
            Result = ScheduledJobConstants.ResultSuccess,
            StartedAt = startedAt,
            TraceId = traceId
        };

        try
        {
            log.OutputSummary = await ExecuteJobAsync(job);
            log.Result = ScheduledJobConstants.ResultSuccess;
            job.LastErrorMessage = null;
        }
        catch (Exception ex)
        {
            log.Result = ScheduledJobConstants.ResultFailed;
            log.ErrorMessage = ex.Message;
            job.LastErrorMessage = ex.Message;
            logger.LogError(ex, "Scheduled job {JobCode} failed with trace {TraceId}", job.JobCode, traceId);
        }
        finally
        {
            stopwatch.Stop();
            log.FinishedAt = DateTime.UtcNow;
            log.DurationMs = stopwatch.ElapsedMilliseconds;
            job.LastResult = log.Result;
            job.LastRunAt = startedAt;

            await scheduledJobLogRepository.InsertAsync(log);
            await scheduledJobRepository.UpdateAsync(job);
        }
    }

    private async Task<string> ExecuteJobAsync(SystemScheduledJobEntity job)
    {
        if (job.JobType == ScheduledJobConstants.JobTypePreset)
        {
            return await presetRunner.RunAsync(job.PresetJobCode ?? string.Empty);
        }

        var callback = ScheduledJobDomainPolicy.Deserialize<HttpCallbackConfigDto>(job.HttpCallbackJson)
            ?? throw new InvalidOperationException("HTTP 回调配置缺失");
        return await httpCallbackClient.SendAsync(callback);
    }
}
