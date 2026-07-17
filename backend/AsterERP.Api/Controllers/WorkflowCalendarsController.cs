using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/calendars")]
public sealed class WorkflowCalendarsController(IWorkflowWorkCalendarAppService workflowCalendarService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowCalendarQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowCalendarService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.WorkflowCalendarEdit)]
    public async Task<IActionResult> SaveAsync([FromBody] WorkflowWorkCalendarUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowCalendarService.SaveAsync(request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.WorkflowCalendarDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await workflowCalendarService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }
}
