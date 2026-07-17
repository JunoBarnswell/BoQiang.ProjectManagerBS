using AsterERP.Contracts.Ai;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai;

public interface IAiModelConfigurationService
{
    Task<GridPageResult<AiProviderDto>> GetProvidersAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiProviderDto>> GetProviderOptionsAsync(CancellationToken cancellationToken = default);

    Task<AiProviderDto> CreateProviderAsync(AiProviderUpsertRequest request, CancellationToken cancellationToken = default);

    Task<AiProviderDto> UpdateProviderAsync(string id, AiProviderUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteProviderAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> TestProviderAsync(string id, CancellationToken cancellationToken = default);

    Task<AiProviderDto> SetProviderStatusAsync(string id, bool enabled, CancellationToken cancellationToken = default);

    Task<GridPageResult<AiModelConfigDto>> GetModelsAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiModelConfigDto>> GetModelOptionsAsync(CancellationToken cancellationToken = default);

    Task<AiModelConfigDto> CreateModelAsync(AiModelConfigUpsertRequest request, CancellationToken cancellationToken = default);

    Task<AiModelConfigDto> UpdateModelAsync(string id, AiModelConfigUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteModelAsync(string id, CancellationToken cancellationToken = default);

    Task<AiModelConfigDto> SetModelStatusAsync(string id, bool enabled, CancellationToken cancellationToken = default);

    Task<AiModelConfigDto> CopyModelAsync(string id, CancellationToken cancellationToken = default);
}
