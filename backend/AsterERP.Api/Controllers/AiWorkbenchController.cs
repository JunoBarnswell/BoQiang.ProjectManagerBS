using AsterERP.Api.Application.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/workbench")]
public sealed class AiWorkbenchController(AiWorkbenchService service) : BaseApiController
{
    [HttpGet("overview")]
    [Permission(PermissionCodes.AiWorkbenchView)]
    public async Task<IActionResult> GetOverviewAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetOverviewAsync(cancellationToken));
    }
}
