using AsterERP.Api.Application.System.ScheduledJobs;
using AsterERP.Shared;
using AsterERP.Contracts.System.ScheduledJobs;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/scheduled-jobs")]
public sealed class SystemScheduledJobController(IScheduledJobService scheduledJobService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemScheduledJobQuery)]
    public async Task<IActionResult> GetPageAsync(
        [FromQuery] GridQuery gridQuery,
        [FromQuery] string? jobType,
        [FromQuery] string? result,
        CancellationToken cancellationToken)
    {
        return ApiOk(await scheduledJobService.GetPageAsync(gridQuery, jobType, result, cancellationToken));
    }

    [HttpGet("summary")]
    [Permission(PermissionCodes.SystemScheduledJobQuery)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await scheduledJobService.GetSummaryAsync(cancellationToken));
    }

    [HttpGet("job-types")]
    [Permission(PermissionCodes.SystemScheduledJobQuery)]
    public async Task<IActionResult> GetTypesAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await scheduledJobService.GetTypesAsync(cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.SystemScheduledJobQuery)]
    public async Task<IActionResult> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await scheduledJobService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.SystemScheduledJobAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] ScheduledJobUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await scheduledJobService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.SystemScheduledJobEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] ScheduledJobUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await scheduledJobService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemScheduledJobDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await scheduledJobService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/pause")]
    [Permission(PermissionCodes.SystemScheduledJobEdit)]
    public async Task<IActionResult> PauseAsync(string id, CancellationToken cancellationToken)
    {
        await scheduledJobService.PauseAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/resume")]
    [Permission(PermissionCodes.SystemScheduledJobEdit)]
    public async Task<IActionResult> ResumeAsync(string id, CancellationToken cancellationToken)
    {
        await scheduledJobService.ResumeAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/trigger")]
    [Permission(PermissionCodes.SystemScheduledJobTrigger)]
    public async Task<IActionResult> TriggerAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await scheduledJobService.TriggerAsync(id, cancellationToken));
    }

    [HttpGet("{id}/logs")]
    [Permission(PermissionCodes.SystemScheduledJobLog)]
    public async Task<IActionResult> GetLogsAsync(
        string id,
        [FromQuery] GridQuery gridQuery,
        [FromQuery] string? result,
        CancellationToken cancellationToken)
    {
        return ApiOk(await scheduledJobService.GetLogsAsync(id, gridQuery, result, cancellationToken));
    }
}
