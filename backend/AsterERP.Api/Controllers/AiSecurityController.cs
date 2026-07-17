using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/security")]
public sealed class AiSecurityController(AiGovernanceService service) : BaseApiController
{
    [HttpGet("policy")]
    [Permission(PermissionCodes.AiSecurityView)]
    public async Task<IActionResult> GetPolicyAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetSecuritySettingsAsync(cancellationToken));
    }

    [HttpPut("policy")]
    [Permission(PermissionCodes.AiSecurityEdit)]
    public async Task<IActionResult> UpdatePolicyAsync([FromBody] AiSecuritySettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.UpdateSecuritySettingsAsync(request, cancellationToken));
    }
}
