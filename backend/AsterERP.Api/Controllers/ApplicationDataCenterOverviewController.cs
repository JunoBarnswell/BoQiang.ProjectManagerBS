using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center")]
public sealed class ApplicationDataCenterOverviewController(ApplicationDataCenterOverviewService overviewService) : BaseApiController
{
    [HttpGet("modules")]
    [Permission(PermissionCodes.AppDataCenterView)]
    public async Task<IActionResult> GetModulesAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await overviewService.GetModulesAsync(cancellationToken));
    }

    [HttpGet("workspace")]
    [Permission(PermissionCodes.AppDataCenterView)]
    public async Task<IActionResult> GetWorkspaceAsync(
        [FromQuery] string? dataSourceId,
        [FromQuery] string? moduleKey,
        CancellationToken cancellationToken)
    {
        return ApiOk(await overviewService.GetWorkspaceAsync(dataSourceId, moduleKey, cancellationToken));
    }

    [HttpGet("type-options")]
    [Permission(PermissionCodes.AppDataCenterView)]
    public IActionResult GetTypeOptions([FromQuery] string? moduleKey)
    {
        return ApiOk(overviewService.GetTypeOptions(moduleKey));
    }

    [HttpGet("templates")]
    [Permission(PermissionCodes.AppDataCenterView)]
    public IActionResult GetTemplates([FromQuery] string? moduleKey)
    {
        return ApiOk(overviewService.GetTemplates(moduleKey));
    }
}
