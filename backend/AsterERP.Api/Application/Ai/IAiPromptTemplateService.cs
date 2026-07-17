using AsterERP.Contracts.Ai;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai;

public interface IAiPromptTemplateService
{
    Task<GridPageResult<AiPromptTemplateDto>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiPromptTemplateDto>> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<AiPromptTemplateDto> CreateAsync(AiPromptTemplateUpsertRequest request, CancellationToken cancellationToken = default);

    Task<AiPromptTemplateDto> UpdateAsync(string id, AiPromptTemplateUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<AiPromptTemplateDto> CopyAsync(string id, CancellationToken cancellationToken = default);

    Task<AiPromptTemplateDto> PublishAsync(string id, AiPromptPublishRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiPromptVersionDto>> GetVersionsAsync(string id, CancellationToken cancellationToken = default);

    Task<AiPromptTestResponse> TestAsync(AiPromptTestRequest request, CancellationToken cancellationToken = default);
}
