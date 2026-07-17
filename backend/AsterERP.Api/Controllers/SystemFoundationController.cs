using AsterERP.Api.Application.System.Foundation;
using AsterERP.Shared;
using AsterERP.Contracts.Logs;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system")]
public sealed class SystemFoundationController(ISystemFoundationService foundationService) : BaseApiController
{
    [HttpGet("dicts/{dictCode}/options")]
    [Permission(PermissionCodes.SystemDictQuery)]
    public async Task<IActionResult> GetDictOptionsAsync(string dictCode, CancellationToken cancellationToken)
    {
        return ApiOk(await foundationService.GetDictOptionsAsync(dictCode, cancellationToken));
    }

    [HttpGet("code-rules/{ruleCode}/preview")]
    [Permission(PermissionCodes.SystemCodeRuleQuery)]
    public async Task<IActionResult> PreviewCodeAsync(string ruleCode, CancellationToken cancellationToken)
    {
        return ApiOk(await foundationService.PreviewCodeAsync(ruleCode, cancellationToken));
    }

    [HttpPost("code-rules/{ruleCode}/generate")]
    [Permission(PermissionCodes.SystemCodeRuleGenerate)]
    public async Task<IActionResult> GenerateCodeAsync(string ruleCode, CancellationToken cancellationToken)
    {
        return ApiOk(await foundationService.GenerateCodeAsync(ruleCode, cancellationToken));
    }

    [HttpGet("operation-logs/recent")]
    [Permission(PermissionCodes.SystemOperationLogQuery)]
    public async Task<IActionResult> RecentOperationLogsAsync([FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        return ApiOk(await foundationService.GetRecentOperationLogsAsync(take, cancellationToken));
    }

    [HttpGet("operation-logs")]
    [Permission(PermissionCodes.SystemOperationLogQuery)]
    public async Task<IActionResult> GetOperationLogsAsync([FromQuery] OperationLogQueryRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await foundationService.GetOperationLogsAsync(request, cancellationToken));
    }

    [HttpGet("operation-logs/{id}")]
    [Permission(PermissionCodes.SystemOperationLogQuery)]
    public async Task<IActionResult> GetOperationLogDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await foundationService.GetOperationLogDetailAsync(id, cancellationToken));
    }
}
