using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/data-center-assistant")]
public sealed class AiDataCenterAssistantController(AiDataCenterAssistantService service) : BaseApiController
{
    [HttpPost("intent")]
    [Permission(PermissionCodes.AiChatCreate)]
    public async Task<IActionResult> ResolveIntentAsync(
        [FromBody] AiDataCenterAssistantIntentRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.ResolveIntentAsync(request, cancellationToken));
    }
}
