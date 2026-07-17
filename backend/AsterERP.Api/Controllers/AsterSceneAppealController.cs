using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/asterscene/moderation")]
public sealed class AsterSceneAppealController(AsterSceneCommerceGovernanceService service) : BaseApiController
{
    [HttpPost("cases/{caseId}/appeals")]
    [Permission(PermissionCodes.AsterSceneModerationAppealCreate)]
    public async Task<IActionResult> CreateAsync(
        string caseId,
        [FromBody] AsterSceneAppealRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.CreateAppealAsync(caseId, request, cancellationToken));
    }
}
