using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/public/asterscene")]
public sealed class PublicAsterSceneController(
    AsterScenePublicService publicService,
    AsterScenePublishService publishService) : BaseApiController
{
    [HttpGet("explore")]
    public async Task<IActionResult> ExploreAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await publicService.ExploreAsync(query, cancellationToken));
    }

    [HttpGet("templates")]
    public async Task<IActionResult> TemplatesAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await publicService.GetTemplatesAsync(query, cancellationToken));
    }

    [HttpGet("works/{slug}")]
    public async Task<IActionResult> GetWorkAsync(string slug, CancellationToken cancellationToken)
    {
        return ApiOk(await publicService.GetWorkBySlugAsync(slug, cancellationToken));
    }

    [HttpGet("creator/{handle}")]
    public async Task<IActionResult> GetCreatorAsync(string handle, CancellationToken cancellationToken)
    {
        return ApiOk(await publicService.GetCreatorProfileAsync(handle, cancellationToken));
    }

    [HttpGet("player/{publishCode}/manifest")]
    public async Task<IActionResult> GetPlayerManifestAsync(string publishCode, CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.GetRuntimeManifestAsync(publishCode, cancellationToken));
    }

    [HttpPost("runtime-events")]
    public async Task<IActionResult> RecordRuntimeEventAsync(
        [FromBody] AsterSceneRuntimeEventRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publicService.RecordRuntimeEventAsync(request, cancellationToken));
    }
}
