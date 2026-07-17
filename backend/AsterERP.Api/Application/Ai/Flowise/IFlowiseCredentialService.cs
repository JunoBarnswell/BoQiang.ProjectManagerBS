using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseCredentialService
{
    Task<GridPageResult<FlowiseResourceDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> CreateAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> UpdateAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> RevealAsync(string id, CancellationToken cancellationToken);
}
