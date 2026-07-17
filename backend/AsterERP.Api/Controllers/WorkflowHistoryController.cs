using AsterERP.Api.Application.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/history")]
public sealed class WorkflowHistoryController(IWorkflowHistoryAppService workflowHistoryService) : BaseApiController
{
    [HttpGet("processes")]
    [Permission(PermissionCodes.WorkflowHistoryQuery)]
    public async Task<IActionResult> GetProcessesAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowHistoryService.GetProcessesAsync(gridQuery, cancellationToken));
    }

    [HttpGet("tasks")]
    [Permission(PermissionCodes.WorkflowHistoryQuery)]
    public async Task<IActionResult> GetTasksAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowHistoryService.GetTasksAsync(gridQuery, cancellationToken));
    }

    [HttpGet("activities")]
    [Permission(PermissionCodes.WorkflowHistoryQuery)]
    public async Task<IActionResult> GetActivitiesAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowHistoryService.GetActivitiesAsync(gridQuery, cancellationToken));
    }

    [HttpGet("variables")]
    [Permission(PermissionCodes.WorkflowHistoryQuery)]
    public async Task<IActionResult> GetVariablesAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowHistoryService.GetVariablesAsync(gridQuery, cancellationToken));
    }

    [HttpGet("processes/{processInstanceId}/identity-links")]
    [Permission(PermissionCodes.WorkflowHistoryQuery)]
    public async Task<IActionResult> GetIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowHistoryService.GetIdentityLinksAsync(processInstanceId, cancellationToken));
    }
}
