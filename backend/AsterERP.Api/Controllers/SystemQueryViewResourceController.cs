using AsterERP.Api.Application.System.QueryViews;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/query-view-resources")]
public sealed class SystemQueryViewResourceController(IQueryViewResourceService resourceService) : BaseApiController
{
    [HttpPost("sync")]
    [Permission(PermissionCodes.SystemQueryViewResource)]
    public async Task<IActionResult> SyncAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await resourceService.SyncAsync(cancellationToken));
    }

    [HttpGet("tables")]
    [Permission(PermissionCodes.SystemQueryViewResource)]
    public async Task<IActionResult> GetTablesAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await resourceService.GetTablesAsync(cancellationToken));
    }

    [HttpPut("tables/{id}/enable")]
    [Permission(PermissionCodes.SystemQueryViewResource)]
    public async Task<IActionResult> EnableTableAsync(string id, CancellationToken cancellationToken)
    {
        await resourceService.SetTableEnabledAsync(id, true, cancellationToken);
        return ApiOk(true);
    }

    [HttpPut("tables/{id}/disable")]
    [Permission(PermissionCodes.SystemQueryViewResource)]
    public async Task<IActionResult> DisableTableAsync(string id, CancellationToken cancellationToken)
    {
        await resourceService.SetTableEnabledAsync(id, false, cancellationToken);
        return ApiOk(true);
    }

    [HttpPut("columns/{id}/enable")]
    [Permission(PermissionCodes.SystemQueryViewResource)]
    public async Task<IActionResult> EnableColumnAsync(string id, CancellationToken cancellationToken)
    {
        await resourceService.SetColumnEnabledAsync(id, true, cancellationToken);
        return ApiOk(true);
    }

    [HttpPut("columns/{id}/disable")]
    [Permission(PermissionCodes.SystemQueryViewResource)]
    public async Task<IActionResult> DisableColumnAsync(string id, CancellationToken cancellationToken)
    {
        await resourceService.SetColumnEnabledAsync(id, false, cancellationToken);
        return ApiOk(true);
    }
}
