using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/conversations")]
public sealed class AiConversationsController(IAiConversationService conversationService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AiChatView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.GetPageAsync(query, cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.AiChatView)]
    public async Task<IActionResult> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.AiChatCreate)]
    public async Task<IActionResult> CreateAsync([FromBody] AiConversationCreateRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AiChatCreate)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] AiConversationUpdateRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id}/status")]
    [Permission(PermissionCodes.AiChatArchive)]
    public async Task<IActionResult> UpdateStatusAsync(string id, [FromBody] AiConversationStatusRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.UpdateStatusAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AiChatDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await conversationService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("{id}/messages")]
    [Permission(PermissionCodes.AiChatView)]
    public async Task<IActionResult> GetMessagesAsync(string id, [FromQuery] GridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.GetMessagesAsync(id, query, cancellationToken));
    }

    [HttpGet("{id}/snapshots")]
    [Permission(PermissionCodes.AiChatView)]
    public async Task<IActionResult> GetSnapshotsAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.GetSnapshotsAsync(id, cancellationToken));
    }

    [HttpPost("{id}/compress")]
    [Permission(PermissionCodes.AiChatCompress)]
    public async Task<IActionResult> CompressAsync(string id, [FromQuery] string? modelConfigId, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.CompressAsync(id, modelConfigId, cancellationToken));
    }

    [HttpPost("messages/{messageId}/feedback")]
    [Permission(PermissionCodes.AiChatView)]
    public async Task<IActionResult> FeedbackAsync(string messageId, [FromBody] AiMessageFeedbackRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.FeedbackAsync(messageId, request, cancellationToken));
    }
}
