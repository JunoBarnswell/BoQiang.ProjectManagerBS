using AsterERP.Api.Application.System.Organizations;
using AsterERP.Shared;
using AsterERP.Contracts.System;
using AsterERP.Contracts.System.Organizations;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/positions")]
public sealed class SystemPositionController(ISystemPositionService positionService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemPositionQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await positionService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.SystemPositionQuery)]
    public async Task<IActionResult> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await positionService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemPositionAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] PositionUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await positionService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemPositionEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] PositionUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await positionService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemPositionDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await positionService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-delete")]
    [Permission(PermissionCodes.SystemPositionDelete)]
    public async Task<IActionResult> BatchDeleteAsync([FromBody] BatchDeleteRequest request, CancellationToken cancellationToken)
    {
        await positionService.BatchDeleteAsync(request.Ids, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("batch-status")]
    [Permission(PermissionCodes.SystemPositionEdit)]
    public async Task<IActionResult> BatchUpdateStatusAsync([FromBody] BatchStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        await positionService.BatchUpdateStatusAsync(request.Ids, request.Status, cancellationToken);
        return ApiOk(true);
    }
}
