using AsterERP.Api.Application.System.OnlineUsers;
using AsterERP.Shared;
using AsterERP.Contracts.System.OnlineUsers;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/online-users")]
public sealed class SystemOnlineUserController(IOnlineUserService onlineUserService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemOnlineUserQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] OnlineUserQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await onlineUserService.GetPageAsync(query, cancellationToken));
    }

    [HttpPost("{sessionId}/force-logout")]
    [Permission(PermissionCodes.SystemOnlineUserKick)]
    public async Task<IActionResult> ForceLogoutAsync(string sessionId, CancellationToken cancellationToken)
    {
        await onlineUserService.ForceLogoutAsync(sessionId, cancellationToken);
        return ApiOk(true);
    }
}
