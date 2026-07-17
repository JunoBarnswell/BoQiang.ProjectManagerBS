using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseCanvasService
{
    Task<IReadOnlyList<FlowiseNodeCatalogItemDto>> GetNodeCatalogAsync(CancellationToken cancellationToken);

    Task<FlowiseCanvasDto> GetByResourceAsync(string resourceId, CancellationToken cancellationToken);

    Task<FlowiseCanvasDto> SaveAsync(FlowiseCanvasUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseCanvasValidationResultDto> ValidateAsync(FlowiseCanvasUpsertRequest request, CancellationToken cancellationToken);
}
