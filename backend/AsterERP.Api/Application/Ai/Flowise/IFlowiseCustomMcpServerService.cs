using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseCustomMcpServerService
{
    Task<GridPageResult<FlowiseCustomMcpServerDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseCustomMcpServerDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseCustomMcpServerDto> CreateAsync(FlowiseCustomMcpServerUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseCustomMcpServerDto> UpdateAsync(string id, FlowiseCustomMcpServerUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseCustomMcpServerAuthorizeResultDto> AuthorizeAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowiseCustomMcpServerToolDto>> GetToolsAsync(string id, CancellationToken cancellationToken);
}
