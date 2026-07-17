using AsterERP.Api.Application.Im;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.Im;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/im/users")]
public sealed class ImUsersController(IImUserDirectoryService userDirectoryService) : BaseApiController
{
    [HttpGet("~/api/im/directory")]
    [Permission(PermissionCodes.ImUserSearch)]
    public async Task<IActionResult> GetDirectoryAsync([FromQuery] string? keyword, CancellationToken cancellationToken)
    {
        return ApiOk(await userDirectoryService.GetDirectoryAsync(keyword, cancellationToken));
    }

    [HttpGet("search")]
    [Permission(PermissionCodes.ImUserSearch)]
    public async Task<IActionResult> SearchAsync([FromQuery] ImUserSearchQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await userDirectoryService.SearchAsync(query, cancellationToken));
    }
}
