using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Integration;

public interface IIntegrationContextService
{
    Task<IntegrationContextEntity?> FindByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task AddIntegrationContextAsync(
        IntegrationContextEntity integrationContextEntity,
        CancellationToken cancellationToken = default);

    Task UpdateIntegrationContextAsync(
        IntegrationContextEntity integrationContextEntity,
        CancellationToken cancellationToken = default);

    Task DeleteIntegrationContextAsync(
        IntegrationContextEntity integrationContextEntity,
        CancellationToken cancellationToken = default);
}
