using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/reminders")]
[Permission(PermissionCodes.ProjectManagementReminderView)]
public sealed class ProjectManagementProjectRemindersController(IProjectManagementProjectReminderService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementReminderManage)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementProjectReminderCreateRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpPost("{id}/cancel")]
    [Permission(PermissionCodes.ProjectManagementReminderManage)]
    public async Task<IActionResult> CancelAsync(string projectId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        try
        {
            await service.CancelAsync(projectId, id, versionNo, cancellationToken);
            return ApiOk(new { id });
        }
        catch (ProjectManagementProjectReminderVersionConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Ok(exception.Conflict, HttpContext.TraceIdentifier, exception.Message));
        }
    }
}
