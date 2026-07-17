using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseMcpServerService
{
    Task<FlowiseMcpServerConfigDto> GetAsync(string chatflowId, CancellationToken cancellationToken);

    Task<FlowiseMcpServerConfigDto> CreateAsync(string chatflowId, FlowiseMcpServerUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseMcpServerConfigDto> UpdateAsync(string chatflowId, FlowiseMcpServerUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseMcpServerConfigDto> DisableAsync(string chatflowId, CancellationToken cancellationToken);

    Task<FlowiseMcpServerConfigDto> RefreshTokenAsync(string chatflowId, CancellationToken cancellationToken);
}
