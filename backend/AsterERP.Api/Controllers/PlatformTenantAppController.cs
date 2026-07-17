using AsterERP.Api.Application.Platform.TenantApps;
using AsterERP.Shared;
using AsterERP.Contracts.Platform;
using AsterERP.Contracts.System;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/platform/tenant-apps")]
public sealed class PlatformTenantAppController(IPlatformTenantAppService tenantAppService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.PlatformTenantAppQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantAppService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.PlatformTenantAppInstall)]
    public async Task<IActionResult> CreateAsync([FromBody] TenantAppUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantAppService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.PlatformTenantAppInstall)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] TenantAppUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantAppService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.PlatformTenantAppUninstall)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await tenantAppService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.PlatformTenantAppInstall)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await tenantAppService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }
}
