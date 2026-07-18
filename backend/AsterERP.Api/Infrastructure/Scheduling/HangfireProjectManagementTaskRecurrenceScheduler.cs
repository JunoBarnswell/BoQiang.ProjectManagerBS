using AsterERP.Api.Application.ProjectManagement;
using Hangfire;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class HangfireProjectManagementTaskRecurrenceScheduler(IRecurringJobManager recurringJobManager) : IProjectManagementTaskRecurrenceScheduler
{
    public Task ScheduleAsync(ProjectManagementTaskRecurrenceGenerationJobArgs args, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        recurringJobManager.AddOrUpdate<ProjectManagementTaskRecurrenceGenerationJob>(
            BuildJobId(args.RecurrenceId),
            "scheduled-jobs",
            job => job.ExecuteAsync(args),
            "0 */15 * * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string recurrenceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        recurringJobManager.RemoveIfExists(BuildJobId(recurrenceId));
        return Task.CompletedTask;
    }

    private static string BuildJobId(string recurrenceId) => $"project-management:recurrence:{recurrenceId}";
}
