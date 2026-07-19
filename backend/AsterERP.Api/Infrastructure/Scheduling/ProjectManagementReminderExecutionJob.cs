using AsterERP.Api.Application.ProjectManagement;
using Hangfire;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ProjectManagementReminderExecutionJob(ProjectManagementReminderExecutionRunner runner)
{
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    [Queue("scheduled-jobs")]
    public Task ExecuteAsync(ProjectManagementReminderJobArgs args) => runner.ExecuteAsync(args, CancellationToken.None);
}
