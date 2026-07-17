using AsterERP.Api.Application.System.Notifications;
using AsterERP.Shared;
using AsterERP.Contracts.System.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/notifications")]
public sealed class SystemNotificationController(INotificationService notificationService) : BaseApiController
{
    [HttpPost("broadcast")]
    [Permission(PermissionCodes.SystemNotificationBroadcast)]
    public async Task<IActionResult> BroadcastAsync([FromBody] NotificationBroadcastRequest request, CancellationToken cancellationToken)
    {
        await notificationService.BroadcastAsync(request.EventName, request.Message, request.Scope, cancellationToken);
        return ApiOk(true);
    }
}
