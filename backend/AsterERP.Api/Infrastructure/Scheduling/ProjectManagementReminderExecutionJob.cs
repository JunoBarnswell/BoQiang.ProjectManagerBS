using AsterERP.Api.Application.ProjectManagement;
using Hangfire;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ProjectManagementReminderExecutionJob(ProjectManagementReminderExecutionRunner runner)
{
    [Queue("scheduled-jobs")]
    public Task ExecuteAsync(ProjectManagementReminderJobArgs args) => runner.ExecuteAsync(args, CancellationToken.None);
}
