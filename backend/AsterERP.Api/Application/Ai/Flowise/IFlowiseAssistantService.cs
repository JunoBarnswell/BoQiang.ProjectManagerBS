using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.Assistants;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseAssistantService
{
    Task<GridPageResult<FlowiseAssistantDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseAssistantDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseAssistantDto> CreateAsync(FlowiseAssistantUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseAssistantDto> UpdateAsync(string id, FlowiseAssistantUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
