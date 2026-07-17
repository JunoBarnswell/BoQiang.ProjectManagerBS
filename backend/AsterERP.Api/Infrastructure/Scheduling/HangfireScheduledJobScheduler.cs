using AsterERP.Api.Application.System.ScheduledJobs;
using AsterERP.Api.Domain.System.ScheduledJobs;
using AsterERP.Api.Modules.System.ScheduledJobs;
using Hangfire;
using Volo.Abp.BackgroundJobs;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class HangfireScheduledJobScheduler(
    IBackgroundJobManager backgroundJobManager,
    IRecurringJobManager recurringJobManager) : IScheduledJobScheduler
{
    private const string QueueName = "scheduled-jobs";

    public Task<string> EnqueueAsync(SystemScheduledJobEntity job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return backgroundJobManager.EnqueueAsync(
            new ScheduledJobExecutionJobArgs(job.Id, ScheduledJobConstants.TriggerManual));
    }

    public Task RegisterOrUpdateAsync(SystemScheduledJobEntity job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        recurringJobManager.AddOrUpdate<ScheduledJobExecutor>(
            BuildRecurringJobId(job),
            QueueName,
            executor => executor.ExecuteAsync(job.Id, ScheduledJobConstants.TriggerAutomatic, null),
            job.CronExpression,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById(job.TimeZoneId)
            });
        return Task.CompletedTask;
    }

    public Task RemoveAsync(SystemScheduledJobEntity job, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        recurringJobManager.RemoveIfExists(BuildRecurringJobId(job));
        return Task.CompletedTask;
    }

    private static string BuildRecurringJobId(SystemScheduledJobEntity job) => $"scheduled-job:{job.Id}";
}
