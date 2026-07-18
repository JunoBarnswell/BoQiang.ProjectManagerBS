using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/activities")]
[Permission(PermissionCodes.ProjectManagementAuditView)]
public sealed class ProjectManagementActivitiesController(IProjectManagementActivityService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(
        string projectId,
        [FromQuery] ProjectManagementActivityQuery query,
        CancellationToken cancellationToken = default)
        => ApiOk(await service.QueryAsync(projectId, query, cancellationToken));

    [HttpGet("~/api/project-management/tasks/{taskId}/activities")]
    public async Task<IActionResult> QueryTaskAsync(
        string taskId,
        [FromQuery] ProjectManagementActivityQuery query,
        CancellationToken cancellationToken = default)
        => ApiOk(await service.QueryTaskAsync(taskId, query, cancellationToken));
}
