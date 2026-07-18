using AsterERP.Api.Application.ProjectManagement;
using Hangfire;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class HangfireProjectManagementReminderScheduler(IBackgroundJobClient backgroundJobClient) : IProjectManagementReminderScheduler
{
    public Task<string> ScheduleAsync(ProjectManagementReminderJobArgs args, DateTimeOffset scheduledAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var effectiveAt = scheduledAt < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : scheduledAt;
        return Task.FromResult(backgroundJobClient.Schedule<ProjectManagementReminderExecutionJob>(job => job.ExecuteAsync(args), effectiveAt));
    }

    public Task DeleteAsync(string? jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(jobId)) backgroundJobClient.Delete(jobId);
        return Task.CompletedTask;
    }
}
