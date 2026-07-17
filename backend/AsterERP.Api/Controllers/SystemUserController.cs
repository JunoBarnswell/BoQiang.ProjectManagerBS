using AsterERP.Api.Application.System.Users;
using AsterERP.Shared;
using AsterERP.Contracts.System;
using AsterERP.Contracts.System.Users;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/users")]
public sealed class SystemUserController(ISystemUserService systemUserService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemUserQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await systemUserService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.SystemUserQuery)]
    public async Task<IActionResult> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await systemUserService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemUserAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] UserUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await systemUserService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemUserEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] UserUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await systemUserService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemUserDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await systemUserService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-delete")]
    [Permission(PermissionCodes.SystemUserDelete)]
    public async Task<IActionResult> BatchDeleteAsync([FromBody] BatchDeleteRequest request, CancellationToken cancellationToken)
    {
        await systemUserService.BatchDeleteAsync(request.Ids, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.SystemUserEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await systemUserService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }

    [HttpPut("{id}/roles")]
    [Permission(PermissionCodes.SystemUserGrantRole)]
    public async Task<IActionResult> UpdateRolesAsync(string id, [FromBody] UserRoleUpdateRequest request, CancellationToken cancellationToken)
    {
        await systemUserService.UpdateRolesAsync(id, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/password")]
    [Permission(PermissionCodes.SystemUserResetPassword)]
    public async Task<IActionResult> ResetPasswordAsync(string id, [FromBody] UserResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await systemUserService.ResetPasswordAsync(id, request, cancellationToken);
        return ApiOk(true);
    }
}
