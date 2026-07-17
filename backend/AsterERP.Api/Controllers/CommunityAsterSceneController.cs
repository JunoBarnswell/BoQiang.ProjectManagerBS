using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/community/asterscene")]
public sealed class CommunityAsterSceneController(
    AsterScenePublicService publicService,
    AsterSceneCommerceGovernanceService commerceGovernanceService) : BaseApiController
{
    [HttpPost("works/{workId}/like")]
    [Permission(PermissionCodes.AsterSceneCommunityInteract)]
    public async Task<IActionResult> LikeAsync(
        string workId,
        [FromBody] AsterSceneReactionRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publicService.ReactAsync(workId, "like", request, cancellationToken));
    }

    [HttpPost("works/{workId}/favorite")]
    [Permission(PermissionCodes.AsterSceneCommunityInteract)]
    public async Task<IActionResult> FavoriteAsync(
        string workId,
        [FromBody] AsterSceneReactionRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publicService.ReactAsync(workId, "favorite", request, cancellationToken));
    }

    [HttpPost("works/{workId}/remix")]
    [Permission(PermissionCodes.AsterSceneRemixCreate)]
    public async Task<IActionResult> RemixAsync(
        string workId,
        [FromBody] AsterSceneRemixRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publicService.RemixAsync(workId, request, cancellationToken));
    }

    [HttpPost("works/{workId}/report")]
    [Permission(PermissionCodes.AsterSceneCommunityInteract)]
    public async Task<IActionResult> ReportAsync(
        string workId,
        [FromBody] AsterSceneModerationReportRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await commerceGovernanceService.ReportWorkAsync(workId, request, cancellationToken));
    }
}
