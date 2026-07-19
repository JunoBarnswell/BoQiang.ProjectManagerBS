using AsterERP.Api.Application.ProjectManagement;
using Volo.Abp.BackgroundJobs;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ProjectManagementWebhookDeliveryJob(ProjectManagementWebhookDeliveryRunner runner) : AsyncBackgroundJob<ProjectManagementWebhookDeliveryJobArgs>
{
    public override Task ExecuteAsync(ProjectManagementWebhookDeliveryJobArgs args) => runner.ExecuteAsync(args, CancellationToken.None);
}
