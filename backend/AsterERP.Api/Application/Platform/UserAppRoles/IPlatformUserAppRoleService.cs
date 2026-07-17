using AsterERP.Shared;
using AsterERP.Contracts.Platform;

namespace AsterERP.Api.Application.Platform.UserAppRoles;

public interface IPlatformUserAppRoleService
{
    Task<GridPageResult<UserAppRoleResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<UserAppRoleResponse> CreateAsync(UserAppRoleUpsertRequest request, CancellationToken cancellationToken = default);

    Task<UserAppRoleResponse> UpdateAsync(string id, UserAppRoleUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
