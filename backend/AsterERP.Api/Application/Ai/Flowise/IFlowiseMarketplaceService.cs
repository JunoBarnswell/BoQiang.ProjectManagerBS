using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseMarketplaceService
{
    Task<GridPageResult<FlowiseResourceDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> CreateAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> CreateFromFlowTemplateAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> UpdateAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
