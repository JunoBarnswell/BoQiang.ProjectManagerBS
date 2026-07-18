namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskRecurrenceScheduler
{
    Task ScheduleAsync(ProjectManagementTaskRecurrenceGenerationJobArgs args, CancellationToken cancellationToken = default);
    Task DeleteAsync(string recurrenceId, CancellationToken cancellationToken = default);
}
