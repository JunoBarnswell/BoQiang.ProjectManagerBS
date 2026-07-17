using AsterERP.Api.Application.Im;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.Im;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/im/conversations")]
public sealed class ImConversationsController(IImConversationService conversationService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.ImConversationView)]
    public async Task<IActionResult> GetConversationsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.GetConversationsAsync(cancellationToken));
    }

    [HttpPost("direct")]
    [Permission(PermissionCodes.ImConversationCreate)]
    public async Task<IActionResult> CreateDirectAsync([FromBody] ImDirectConversationRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.CreateDirectConversationAsync(request.TargetUserId, cancellationToken));
    }

    [HttpGet("{conversationId}/messages")]
    [Permission(PermissionCodes.ImMessageRead)]
    public async Task<IActionResult> GetMessagesAsync(string conversationId, [FromQuery] string? cursor, [FromQuery] int take, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.GetMessagesAsync(conversationId, cursor, take <= 0 ? 50 : take, cancellationToken));
    }

    [HttpPost("{conversationId}/messages")]
    [Permission(PermissionCodes.ImMessageSend)]
    public async Task<IActionResult> SendMessageAsync(string conversationId, [FromBody] ImSendMessageRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.SendMessageAsync(conversationId, request, cancellationToken));
    }

    [HttpPost("{conversationId}/read")]
    [Permission(PermissionCodes.ImMessageRead)]
    public async Task<IActionResult> MarkReadAsync(string conversationId, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.MarkReadAsync(conversationId, cancellationToken));
    }
}
