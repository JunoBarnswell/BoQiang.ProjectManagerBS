using AsterERP.Api.Application.Im;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/im/unread-summary")]
public sealed class ImUnreadController(IImConversationService conversationService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.ImMessageRead)]
    public async Task<IActionResult> GetUnreadSummaryAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await conversationService.GetUnreadSummaryAsync(cancellationToken));
    }
}
