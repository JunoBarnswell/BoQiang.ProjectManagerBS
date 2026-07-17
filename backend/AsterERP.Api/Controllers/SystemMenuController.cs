using AsterERP.Api.Application.System.Menus;
using AsterERP.Shared;
using AsterERP.Contracts.System;
using AsterERP.Contracts.System.Menus;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/menus")]
public sealed class SystemMenuController(ISystemMenuService systemMenuService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemMenuQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await systemMenuService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("tree")]
    [Permission(PermissionCodes.SystemMenuQuery)]
    public async Task<IActionResult> GetTreeAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await systemMenuService.GetTreeAsync(gridQuery, cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.SystemMenuQuery)]
    public async Task<IActionResult> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await systemMenuService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemMenuAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] MenuUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await systemMenuService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemMenuEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] MenuUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await systemMenuService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemMenuDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await systemMenuService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-delete")]
    [Permission(PermissionCodes.SystemMenuDelete)]
    public async Task<IActionResult> BatchDeleteAsync([FromBody] BatchDeleteRequest request, CancellationToken cancellationToken)
    {
        await systemMenuService.BatchDeleteAsync(request.Ids, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.SystemMenuEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await systemMenuService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }
}
