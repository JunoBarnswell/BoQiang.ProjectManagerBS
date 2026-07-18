namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementReminderScheduler
{
    Task<string> ScheduleAsync(ProjectManagementReminderJobArgs args, DateTimeOffset scheduledAt, CancellationToken cancellationToken = default);
    Task DeleteAsync(string? jobId, CancellationToken cancellationToken = default);
}
