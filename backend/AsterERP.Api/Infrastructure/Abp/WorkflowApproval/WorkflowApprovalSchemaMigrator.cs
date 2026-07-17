using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Approval.Core.Configuration;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.WorkflowApproval;

public sealed class WorkflowApprovalSchemaMigrator
{
    public async Task MigrateAsync(
        IServiceProvider serviceProvider,
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WorkflowGlobalListenerConfig.RegisterGlobalListeners(serviceProvider);

        var configuration = serviceProvider.GetRequiredService<IProcessEngineConfiguration>();
        await serviceProvider
            .GetRequiredService<IWorkflowPersistenceStore>()
            .InitializeAsync(configuration, cancellationToken);

        serviceProvider.GetRequiredService<WorkflowApprovalSchemaInitializer>().Initialize(db);
    }
}
