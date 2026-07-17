using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/admin/asterscene/moderation")]
public sealed class AsterSceneModerationAdminController(AsterSceneCommerceGovernanceService service) : BaseApiController
{
    [HttpGet("cases")]
    [Permission(PermissionCodes.AsterSceneAdminView)]
    public async Task<IActionResult> GetCasesAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetModerationCasesAsync(query, cancellationToken));
    }

    [HttpGet("cases/{caseId}")]
    [Permission(PermissionCodes.AsterSceneAdminView)]
    public async Task<IActionResult> GetCaseAsync(string caseId, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetModerationCaseDetailAsync(caseId, cancellationToken));
    }

    [HttpPost("cases/{caseId}/decision")]
    [Permission(PermissionCodes.AsterSceneModerationManage)]
    public async Task<IActionResult> DecideAsync(
        string caseId,
        [FromBody] AsterSceneModerationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.DecideModerationAsync(caseId, request, cancellationToken));
    }

    [HttpGet("appeals")]
    [Permission(PermissionCodes.AsterSceneAppealManage)]
    public async Task<IActionResult> GetAppealsAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetAppealsAsync(query, cancellationToken));
    }

    [HttpPost("appeals/{appealId}/decision")]
    [Permission(PermissionCodes.AsterSceneAppealManage)]
    public async Task<IActionResult> DecideAppealAsync(
        string appealId,
        [FromBody] AsterSceneAppealDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.DecideAppealAsync(appealId, request, cancellationToken));
    }
}
