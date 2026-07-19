using AsterERP.Api.Application.ProjectManagement;
using Hangfire;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ProjectManagementAutomationWebhookJob(ProjectManagementAutomationRunner runner)
{
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [Queue("scheduled-jobs")]
    public Task ExecuteAsync(ProjectManagementAutomationWebhookJobArgs args) => runner.ExecuteAsync(args, CancellationToken.None);
}
