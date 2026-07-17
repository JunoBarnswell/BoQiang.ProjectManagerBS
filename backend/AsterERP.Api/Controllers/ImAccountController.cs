using AsterERP.Api.Application.Im;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/im/account-binding")]
public sealed class ImAccountController(IImAccountBindingService accountBindingService) : BaseApiController
{
    [HttpGet("me")]
    [Permission(PermissionCodes.ImConversationView)]
    public async Task<IActionResult> GetCurrentAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await accountBindingService.GetCurrentAsync(cancellationToken));
    }
}
