using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseNodeCatalogService
{
    Task<IReadOnlyList<FlowiseNodeDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken);

    Task<FlowiseNodeIcon> GetNodeIconAsync(string name, CancellationToken cancellationToken);
}
