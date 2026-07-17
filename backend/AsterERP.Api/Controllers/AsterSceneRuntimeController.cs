using AsterERP.Api.Application.AsterScene;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/asterscene/runtime")]
public sealed class AsterSceneRuntimeController(AsterScenePublishService publishService) : BaseApiController
{
    [HttpGet("{publishCode}/manifest")]
    public async Task<IActionResult> GetManifestAsync(string publishCode, CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.GetRuntimeManifestAsync(publishCode, cancellationToken));
    }
}
