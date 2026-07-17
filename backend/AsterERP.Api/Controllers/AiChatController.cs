using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/chat")]
public sealed class AiChatController(IAiStreamService streamService) : BaseApiController
{
    [HttpPost("conversations/{conversationId}/stream")]
    [Permission(PermissionCodes.AiChatCreate)]
    public async Task StreamAsync(
        string conversationId,
        [FromBody] AiChatStreamRequest request,
        CancellationToken cancellationToken)
    {
        await streamService.StreamAsync(conversationId, request, Response, HttpContext.TraceIdentifier, cancellationToken);
    }

    [HttpPost("runs/{runId}/stop")]
    [Permission(PermissionCodes.AiChatCreate)]
    public async Task<IActionResult> StopRunAsync(string runId, CancellationToken cancellationToken)
    {
        return ApiOk(await streamService.StopRunAsync(runId, cancellationToken));
    }
}
