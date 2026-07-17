using AsterERP.Contracts.Ai;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai;

public interface IAiAgentProfileService
{
    Task<GridPageResult<AiAgentProfileDto>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAgentProfileDto>> GetOptionsAsync(CancellationToken cancellationToken = default);

    Task<AiAgentProfileDto> CreateAsync(AiAgentProfileUpsertRequest request, CancellationToken cancellationToken = default);

    Task<AiAgentProfileDto> UpdateAsync(string id, AiAgentProfileUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<AiAgentProfileDto> CopyAsync(string id, CancellationToken cancellationToken = default);

    Task<AiAgentProfileDto> SetStatusAsync(string id, bool enabled, CancellationToken cancellationToken = default);

    Task<bool> TestAsync(string id, CancellationToken cancellationToken = default);
}
