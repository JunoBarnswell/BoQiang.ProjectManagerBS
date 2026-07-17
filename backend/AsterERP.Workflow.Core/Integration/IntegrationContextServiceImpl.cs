using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Integration;

public class IntegrationContextServiceImpl : IIntegrationContextService
{
    private readonly ICommandExecutor _commandExecutor;

    public IntegrationContextServiceImpl(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public Task<IntegrationContextEntity?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return _commandExecutor.ExecuteAsync(
            new RetrieveIntegrationContextCmd(id),
            cancellationToken);
    }

    public Task AddIntegrationContextAsync(
        IntegrationContextEntity integrationContextEntity,
        CancellationToken cancellationToken = default)
    {
        return _commandExecutor.ExecuteAsync(
            new AddIntegrationContextCmd(integrationContextEntity),
            cancellationToken);
    }

    public Task UpdateIntegrationContextAsync(
        IntegrationContextEntity integrationContextEntity,
        CancellationToken cancellationToken = default)
    {
        return _commandExecutor.ExecuteAsync(
            new UpdateIntegrationContextCmd(integrationContextEntity),
            cancellationToken);
    }

    public Task DeleteIntegrationContextAsync(
        IntegrationContextEntity integrationContextEntity,
        CancellationToken cancellationToken = default)
    {
        return _commandExecutor.ExecuteAsync(
            new DeleteIntegrationContextCmd(integrationContextEntity),
            cancellationToken);
    }
}
