using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseManagementService
{
    Task<FlowiseOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowiseResourceTypeDto>> GetResourceTypesAsync(CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseWorkspaceDto>> GetWorkspacesAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseWorkspaceDto> UpsertWorkspaceAsync(string? id, FlowiseWorkspaceUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteWorkspaceAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowiseSharedWorkspaceDto>> GetSharedWorkspacesAsync(string itemId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowiseSharedWorkspaceDto>> SetSharedWorkspacesAsync(string itemId, FlowiseShareWorkspacesRequest request, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseResourceDto>> GetSsoConfigsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> CreateSsoConfigAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> UpdateSsoConfigAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteSsoConfigAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseSsoConfigDto?> GetSsoConfigDetailAsync(CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseResourceDto>> GetRolesAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> CreateRoleAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> UpdateRoleAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteRoleAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseRoleDto> GetRoleDetailAsync(string id, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseResourceDto>> GetUsersAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> CreateUserAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> UpdateUserAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseUserDto> GetUserDetailAsync(string id, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseResourceDto>> GetLoginActivityResourcesAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> CreateLoginActivityAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> UpdateLoginActivityAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteLoginActivityAsync(string id, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseLoginActivityDto>> GetLoginActivityAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseResourceDto>> GetLogResourcesAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> CreateLogAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseResourceDto> UpdateLogAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteLogAsync(string id, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseAuditLogDto>> GetLogsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken);

    Task<FlowiseAccountSettingsDto> GetAccountAsync(CancellationToken cancellationToken);

    Task<FlowiseAccountSettingsDto> UpdateAccountAsync(FlowiseAccountSettingsDto request, CancellationToken cancellationToken);
}
