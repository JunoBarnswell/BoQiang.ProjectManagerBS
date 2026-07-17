using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Api.Modules.Ai.Flowise;
using Hangfire;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class HangfireFlowiseScheduleScheduler(
    IRecurringJobManager recurringJobManager) : IFlowiseScheduleScheduler
{
    private const string QueueName = "scheduled-jobs";

    public Task ApplyAsync(FlowiseScheduleRecordEntity? record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var jobId = BuildJobId(record?.Id);
        if (record is null || record.IsDeleted || !record.Enabled || record.EndDate is not null && record.EndDate.Value <= DateTime.UtcNow)
        {
            recurringJobManager.RemoveIfExists(jobId);
            return Task.CompletedTask;
        }

        recurringJobManager.AddOrUpdate<FlowiseScheduleExecutionJob>(
            jobId,
            QueueName,
            job => job.ExecuteAsync(new FlowiseScheduleExecutionJobArgs(record.Id, record.TenantId, record.AppCode, record.OwnerUserId)),
            record.CronExpression,
            new RecurringJobOptions
            {
                TimeZone = ResolveTimeZone(record.Timezone)
            });
        return Task.CompletedTask;
    }

    private static string BuildJobId(string? recordId) => $"flowise-schedule:{recordId ?? "missing"}";

    private static TimeZoneInfo ResolveTimeZone(string timezone) =>
        TimeZoneInfo.FindSystemTimeZoneById(timezone);
}
