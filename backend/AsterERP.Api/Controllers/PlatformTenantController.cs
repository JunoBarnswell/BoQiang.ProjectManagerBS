using AsterERP.Api.Application.Platform.Tenants;
using AsterERP.Shared;
using AsterERP.Contracts.Platform;
using AsterERP.Contracts.System;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/platform/tenants")]
public sealed class PlatformTenantController(IPlatformTenantService tenantService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.PlatformTenantQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.PlatformTenantAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] TenantUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.PlatformTenantEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] TenantUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await tenantService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.PlatformTenantDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await tenantService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.PlatformTenantEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await tenantService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }
}
