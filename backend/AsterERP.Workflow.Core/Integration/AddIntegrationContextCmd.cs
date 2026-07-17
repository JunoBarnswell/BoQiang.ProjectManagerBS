using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Integration;

internal class AddIntegrationContextCmd : IntegrationContextCommandBase, ICommand<object?>
{
    private readonly IntegrationContextEntity _entity;

    public AddIntegrationContextCmd(IntegrationContextEntity entity)
    {
        _entity = entity;
    }

    public object? Execute(ICommandContext context)
    {
        throw SyncExecutionNotSupported();
    }

    public async Task<object?> ExecuteAsync(
        ICommandContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_entity.Id))
        {
            _entity.Id = AbpTimeIdProvider.NewGuid("N");
        }

        if (_entity.CreatedDate == null)
        {
            _entity.CreatedDate = AbpTimeIdProvider.UtcNow;
        }

        await ResolveStore(context).UpsertIntegrationContextAsync(_entity, cancellationToken);
        return null;
    }
}
