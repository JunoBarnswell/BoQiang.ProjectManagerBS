using AsterERP.Api.Application.System.Roles;
using AsterERP.Shared;
using AsterERP.Contracts.System;
using AsterERP.Contracts.System.Roles;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/roles")]
public sealed class SystemRoleController(ISystemRoleService systemRoleService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemRoleQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await systemRoleService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("permissions")]
    [Permission(PermissionCodes.SystemRoleQuery)]
    public async Task<IActionResult> GetPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await systemRoleService.GetPermissionCatalogAsync(cancellationToken));
    }

    [HttpGet("{id}/permissions")]
    [Permission(PermissionCodes.SystemRoleQuery)]
    public async Task<IActionResult> GetRolePermissionCodesAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await systemRoleService.GetRolePermissionCodesAsync(id, cancellationToken));
    }

    [HttpGet("permission-tree")]
    [Permission(PermissionCodes.SystemRoleQuery)]
    public async Task<IActionResult> GetPermissionTreeAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await systemRoleService.GetPermissionTreeAsync(gridQuery, cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.SystemRoleQuery)]
    public async Task<IActionResult> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await systemRoleService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemRoleAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] RoleUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await systemRoleService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemRoleEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] RoleUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await systemRoleService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemRoleDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await systemRoleService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-delete")]
    [Permission(PermissionCodes.SystemRoleDelete)]
    public async Task<IActionResult> BatchDeleteAsync([FromBody] BatchDeleteRequest request, CancellationToken cancellationToken)
    {
        await systemRoleService.BatchDeleteAsync(request.Ids, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.SystemRoleEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await systemRoleService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }

    [HttpPut("{id}/permissions")]
    [Permission(PermissionCodes.SystemRoleGrant)]
    public async Task<IActionResult> UpdatePermissionsAsync(string id, [FromBody] RolePermissionUpdateRequest request, CancellationToken cancellationToken)
    {
        await systemRoleService.UpdatePermissionsAsync(id, request, cancellationToken);
        return ApiOk(true);
    }
}
