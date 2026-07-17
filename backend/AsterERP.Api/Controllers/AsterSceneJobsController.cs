using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/asterscene/jobs")]
public sealed class AsterSceneJobsController(
    AsterSceneAssetService assetService,
    AsterSceneCommerceGovernanceService commerceGovernanceService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AsterSceneJobView)]
    public async Task<IActionResult> GetJobsAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await assetService.GetJobsAsync(query, cancellationToken));
    }

    [HttpPost("ai-generate")]
    [Permission(PermissionCodes.AsterSceneAiGenerate)]
    public async Task<IActionResult> CreateAiGenerateJobAsync(
        [FromBody] AsterSceneAiGenerateRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await commerceGovernanceService.CreateAiGenerateJobAsync(request, cancellationToken));
    }
}
