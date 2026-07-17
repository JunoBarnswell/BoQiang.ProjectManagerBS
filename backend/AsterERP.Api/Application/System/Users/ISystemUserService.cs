using AsterERP.Shared;
using AsterERP.Contracts.System.Users;

namespace AsterERP.Api.Application.System.Users;

public interface ISystemUserService
{
    Task<GridPageResult<UserListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<UserListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<UserListItemResponse> CreateAsync(UserUpsertRequest request, CancellationToken cancellationToken = default);

    Task<UserListItemResponse> UpdateAsync(string id, UserUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);

    Task UpdateRolesAsync(string id, UserRoleUpdateRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(string id, UserResetPasswordRequest request, CancellationToken cancellationToken = default);
}
