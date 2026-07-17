using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise/canvas")]
public sealed class AiFlowiseCanvasController(IFlowiseCanvasService canvasService) : BaseApiController
{
    [HttpPost("validate")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> ValidateAsync([FromBody] FlowiseCanvasUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await canvasService.ValidateAsync(request, cancellationToken));
    }
}
