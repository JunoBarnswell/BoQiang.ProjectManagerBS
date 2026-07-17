using AsterERP.Contracts.Platform;
using AsterERP.Contracts.Tenant;

namespace AsterERP.Api.Application.Tenant.Apps;

public interface ITenantAppService
{
    Task<IReadOnlyList<TenantAppCatalogItemResponse>> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantAppListItemResponse>> GetInstalledAsync(CancellationToken cancellationToken = default);

    Task<TenantAppListItemResponse> InstallAsync(string appCode, TenantAppInstallRequest request, CancellationToken cancellationToken = default);

    Task<TenantAppListItemResponse> EnableAsync(string appCode, CancellationToken cancellationToken = default);

    Task<TenantAppListItemResponse> DisableAsync(string appCode, CancellationToken cancellationToken = default);
}
