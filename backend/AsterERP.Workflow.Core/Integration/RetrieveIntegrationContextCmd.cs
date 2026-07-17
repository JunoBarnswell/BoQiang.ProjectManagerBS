using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Integration;

internal class RetrieveIntegrationContextCmd : IntegrationContextCommandBase, ICommand<IntegrationContextEntity?>
{
    private readonly string _id;

    public RetrieveIntegrationContextCmd(string id)
    {
        _id = id;
    }

    public IntegrationContextEntity? Execute(ICommandContext context)
    {
        throw SyncExecutionNotSupported();
    }

    public Task<IntegrationContextEntity?> ExecuteAsync(
        ICommandContext context,
        CancellationToken cancellationToken = default)
    {
        return ResolveStore(context).GetIntegrationContextAsync(_id, cancellationToken);
    }
}
