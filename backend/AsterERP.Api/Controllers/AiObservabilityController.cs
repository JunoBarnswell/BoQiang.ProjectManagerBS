using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/observability")]
public sealed class AiObservabilityController(AiObservabilityService service) : BaseApiController
{
    [HttpGet("summary")]
    [Permission(PermissionCodes.AiObservabilityView)]
    public async Task<IActionResult> GetSummaryAsync([FromQuery] AiUsageQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetSummaryAsync(query, cancellationToken));
    }

    [HttpGet("trends")]
    [Permission(PermissionCodes.AiObservabilityView)]
    public async Task<IActionResult> GetTrendsAsync([FromQuery] AiUsageQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetTrendsAsync(query, cancellationToken));
    }

    [HttpGet("runs")]
    [Permission(PermissionCodes.AiObservabilityView)]
    public async Task<IActionResult> GetRunsAsync([FromQuery] GridQuery gridQuery, [FromQuery] AiObservabilityRunQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetRunsAsync(gridQuery, query, cancellationToken));
    }

    [HttpGet("runs/{runId}")]
    [Permission(PermissionCodes.AiObservabilityView)]
    public async Task<IActionResult> GetRunDetailAsync(string runId, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetRunDetailAsync(runId, cancellationToken));
    }

    [HttpGet("tool-executions")]
    [Permission(PermissionCodes.AiObservabilityView)]
    public async Task<IActionResult> GetToolExecutionsAsync([FromQuery] AiToolExecutionQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetToolExecutionsAsync(query, cancellationToken));
    }

    [HttpGet("failures")]
    [Permission(PermissionCodes.AiObservabilityView)]
    public async Task<IActionResult> GetFailuresAsync([FromQuery] AiUsageQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetFailuresAsync(query, cancellationToken));
    }
}
