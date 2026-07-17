using AsterERP.Api.Application.Platform.UserAppRoles;
using AsterERP.Shared;
using AsterERP.Contracts.Platform;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/platform/user-app-roles")]
public sealed class PlatformUserAppRoleController(IPlatformUserAppRoleService userAppRoleService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.PlatformUserAppRoleQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await userAppRoleService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.PlatformUserAppRoleEdit)]
    public async Task<IActionResult> CreateAsync([FromBody] UserAppRoleUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await userAppRoleService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.PlatformUserAppRoleEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] UserAppRoleUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await userAppRoleService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.PlatformUserAppRoleEdit)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await userAppRoleService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }
}
