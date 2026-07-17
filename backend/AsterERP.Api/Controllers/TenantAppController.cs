using AsterERP.Api.Application.Tenant.Apps;
using AsterERP.Shared;
using AsterERP.Contracts.Tenant;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/tenant/apps")]
public sealed class TenantAppController(ITenantAppService tenantAppService) : BaseApiController
{
    [HttpGet("catalog")]
    [TenantAdminPermission(PermissionCodes.TenantAppQuery)]
    public async Task<IActionResult> GetCatalogAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await tenantAppService.GetCatalogAsync(cancellationToken));
    }

    [HttpGet]
    [TenantAdminPermission(PermissionCodes.TenantAppQuery)]
    public async Task<IActionResult> GetInstalledAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await tenantAppService.GetInstalledAsync(cancellationToken));
    }

    [HttpPost("{appCode}/install")]
    [TenantAdminPermission(PermissionCodes.TenantAppInstall)]
    public async Task<IActionResult> InstallAsync(string appCode, [FromBody] TenantAppInstallRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantAppService.InstallAsync(appCode, request, cancellationToken));
    }

    [HttpPost("{appCode}/enable")]
    [TenantAdminPermission(PermissionCodes.TenantAppEnable)]
    public async Task<IActionResult> EnableAsync(string appCode, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantAppService.EnableAsync(appCode, cancellationToken));
    }

    [HttpPost("{appCode}/disable")]
    [TenantAdminPermission(PermissionCodes.TenantAppDisable)]
    public async Task<IActionResult> DisableAsync(string appCode, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantAppService.DisableAsync(appCode, cancellationToken));
    }
}
