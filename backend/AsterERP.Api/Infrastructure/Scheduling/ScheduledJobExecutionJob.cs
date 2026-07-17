using Volo.Abp.BackgroundJobs;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ScheduledJobExecutionJob(
    ScheduledJobExecutor executor) : IAsyncBackgroundJob<ScheduledJobExecutionJobArgs>
{
    public Task ExecuteAsync(ScheduledJobExecutionJobArgs args) =>
        executor.ExecuteAsync(args.JobId, args.Trigger, null);
}
