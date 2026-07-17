using AsterERP.Api.Application.Platform.UserTenants;
using AsterERP.Shared;
using AsterERP.Contracts.Platform;
using AsterERP.Contracts.System;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/platform/user-tenants")]
public sealed class PlatformUserTenantController(IPlatformUserTenantService userTenantService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.PlatformUserTenantQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await userTenantService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.PlatformUserTenantEdit)]
    public async Task<IActionResult> CreateAsync([FromBody] UserTenantMembershipUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await userTenantService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.PlatformUserTenantEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] UserTenantMembershipUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await userTenantService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.PlatformUserTenantEdit)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await userTenantService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.PlatformUserTenantEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await userTenantService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }
}
