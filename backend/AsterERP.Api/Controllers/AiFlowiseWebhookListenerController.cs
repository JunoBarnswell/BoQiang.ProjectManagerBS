using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/v1")]
public sealed class AiFlowiseWebhookListenerController(IFlowiseWebhookListenerService webhookListenerService) : BaseApiController
{
    [HttpPost("webhook-listener/{chatflowId}")]
    [Permission(PermissionCodes.FlowiseWebhook)]
    public async Task<IActionResult> RegisterAsync([FromRoute] string chatflowId, CancellationToken cancellationToken)
    {
        return ApiOk(await webhookListenerService.RegisterAsync(chatflowId, cancellationToken));
    }

    [HttpGet("webhook-listener/{chatflowId}/stream/{listenerId}")]
    [Permission(PermissionCodes.FlowiseWebhook)]
    public async Task StreamAsync([FromRoute] string chatflowId, [FromRoute] string listenerId, CancellationToken cancellationToken)
    {
        await webhookListenerService.StreamAsync(chatflowId, listenerId, Response, cancellationToken);
    }

    [HttpDelete("webhook-listener/{chatflowId}/{listenerId}")]
    [Permission(PermissionCodes.FlowiseWebhook)]
    public async Task<IActionResult> UnregisterAsync([FromRoute] string chatflowId, [FromRoute] string listenerId, CancellationToken cancellationToken)
    {
        return ApiOk(await webhookListenerService.UnregisterAsync(chatflowId, listenerId, cancellationToken));
    }

    [HttpPost("webhook/{chatflowId}")]
    [Permission(PermissionCodes.FlowiseWebhook)]
    public async Task<IActionResult> TriggerAsync(
        [FromRoute] string chatflowId,
        [FromBody] FlowiseWebhookTriggerRequest request,
        CancellationToken cancellationToken)
    {
        var webhookSecret = Request.Headers["x-flowise-webhook-secret"].FirstOrDefault()
            ?? Request.Headers["x-webhook-secret"].FirstOrDefault();
        return ApiOk(await webhookListenerService.TriggerAsync(chatflowId, request, webhookSecret, cancellationToken));
    }
}
