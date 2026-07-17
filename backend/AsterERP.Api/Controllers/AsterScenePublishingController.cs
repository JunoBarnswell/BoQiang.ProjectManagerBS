using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/asterscene/projects/{projectId}/publish")]
public sealed class AsterScenePublishingController(AsterScenePublishService publishService) : BaseApiController
{
    [HttpGet("versions")]
    [Permission(PermissionCodes.AsterScenePublishView)]
    public async Task<IActionResult> GetPublishVersionsAsync(
        string projectId,
        [FromQuery] AsterSceneGridQuery query,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.GetPublishVersionsAsync(projectId, query, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.AsterScenePublishExecute)]
    public async Task<IActionResult> PublishAsync(
        string projectId,
        [FromBody] AsterScenePublishRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.PublishAsync(projectId, request, cancellationToken));
    }

    [HttpPost("{publishCode}/rollback")]
    [Permission(PermissionCodes.AsterScenePublishRollback)]
    public async Task<IActionResult> RollbackAsync(
        string projectId,
        string publishCode,
        [FromBody] AsterSceneRollbackRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.RollbackAsync(projectId, publishCode, request, cancellationToken));
    }
}
