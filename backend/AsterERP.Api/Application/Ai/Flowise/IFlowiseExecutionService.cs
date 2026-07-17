using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Api.Modules.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseExecutionService
{
    Task<GridPageResult<FlowiseExecutionDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseExecutionDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseExecutionDto> StartAsync(FlowiseExecutionStartRequest request, CancellationToken cancellationToken);

    Task<FlowiseExecutionDto> StartMcpAsync(
        FlowiseChatFlowEntity chatflow,
        string inputJson,
        string? question,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<FlowiseExecutionDto> StreamAsync(
        FlowiseExecutionStartRequest request,
        Func<string, object?, CancellationToken, Task> emitAsync,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
