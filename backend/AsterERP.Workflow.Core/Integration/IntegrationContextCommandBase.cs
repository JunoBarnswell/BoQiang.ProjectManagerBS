using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Integration;

internal abstract class IntegrationContextCommandBase
{
    protected static IWorkflowPersistenceStore ResolveStore(ICommandContext context)
    {
        var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(
            context.ProcessEngineConfiguration);
        if (store?.IsEnabled != true)
        {
            throw new WorkflowEngineException("Integration context persistence store is not enabled.");
        }

        return store;
    }

    protected static NotSupportedException SyncExecutionNotSupported()
    {
        return new NotSupportedException(
            "Integration context commands must be executed through ExecuteAsync.");
    }
}
