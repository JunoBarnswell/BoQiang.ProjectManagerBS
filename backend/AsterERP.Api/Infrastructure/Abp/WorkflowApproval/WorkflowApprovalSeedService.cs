using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Workflow.Approval.Core.Services.Privilege;

namespace AsterERP.Api.Infrastructure.Abp.WorkflowApproval;

public sealed class WorkflowApprovalSeedService(
    SystemInitializer systemInitializer,
    WorkflowIdentitySyncService workflowIdentitySyncService)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await systemInitializer.InitializeAsync(cancellationToken);
        await workflowIdentitySyncService.SyncAsync(cancellationToken);
    }
}
