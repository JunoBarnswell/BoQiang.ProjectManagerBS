using AsterERP.Shared;
using AsterERP.Contracts.Platform;

namespace AsterERP.Api.Application.Platform.TenantApps;

public interface IPlatformTenantAppService
{
    Task<GridPageResult<TenantAppListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<TenantAppListItemResponse> CreateAsync(TenantAppUpsertRequest request, CancellationToken cancellationToken = default);

    Task<TenantAppListItemResponse> UpdateAsync(string id, TenantAppUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);
}
