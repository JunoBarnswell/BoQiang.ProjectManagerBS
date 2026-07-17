using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseMcpEndpointService
{
    Task<FlowiseMcpJsonRpcResponse> HandleAsync(
        string chatflowId,
        string token,
        FlowiseMcpJsonRpcRequest request,
        CancellationToken cancellationToken);
}
