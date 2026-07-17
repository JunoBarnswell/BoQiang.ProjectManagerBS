using AsterERP.Api.Application.System.LoginLogs;
using AsterERP.Shared;
using AsterERP.Contracts.Logs;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/login-logs")]
public sealed class SystemLoginLogController(ILoginLogService loginLogService) : BaseApiController
{
    private const string SystemLoginLogQueryPermission = "system:login-log:query";

    [HttpGet]
    [Permission(SystemLoginLogQueryPermission)]
    public async Task<IActionResult> GetPageAsync([FromQuery] LoginLogQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await loginLogService.GetPageAsync(query, cancellationToken));
    }
}
