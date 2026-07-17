using AsterERP.Shared;
using AsterERP.Contracts.Platform;

namespace AsterERP.Api.Application.Platform.Tenants;

public interface IPlatformTenantService
{
    Task<GridPageResult<TenantListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<TenantListItemResponse> CreateAsync(TenantUpsertRequest request, CancellationToken cancellationToken = default);

    Task<TenantListItemResponse> UpdateAsync(string id, TenantUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);
}
