using AsterERP.Api.Application.ProjectManagement;
using Hangfire;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ProjectManagementTaskRecurrenceGenerationJob(ProjectManagementTaskRecurrenceGenerationRunner runner)
{
    [Queue("scheduled-jobs")]
    public Task ExecuteAsync(ProjectManagementTaskRecurrenceGenerationJobArgs args) => runner.ExecuteAsync(args, CancellationToken.None);
}
