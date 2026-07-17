using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/usage/asterscene")]
public sealed class AsterSceneUsageController(AsterSceneCommerceGovernanceService service) : BaseApiController
{
    [HttpGet("summary")]
    [Permission(PermissionCodes.AsterSceneUsageView)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetUsageSummaryAsync(cancellationToken));
    }

    [HttpGet("ledger")]
    [Permission(PermissionCodes.AsterSceneUsageView)]
    public async Task<IActionResult> GetLedgerAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetUsageLedgerAsync(query, cancellationToken));
    }
}
