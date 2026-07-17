using AsterERP.Shared;
using AsterERP.Contracts.Platform;

namespace AsterERP.Api.Application.Platform.UserTenants;

public interface IPlatformUserTenantService
{
    Task<GridPageResult<UserTenantMembershipResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<UserTenantMembershipResponse> CreateAsync(UserTenantMembershipUpsertRequest request, CancellationToken cancellationToken = default);

    Task<UserTenantMembershipResponse> UpdateAsync(string id, UserTenantMembershipUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);
}
