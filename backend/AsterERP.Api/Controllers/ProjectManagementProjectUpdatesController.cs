using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/updates")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementProjectUpdatesController(
    IProjectManagementActivityService activityService,
    IProjectManagementProjectUpdateService updateService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, [FromQuery] ProjectManagementActivityQuery query, CancellationToken cancellationToken) =>
        ApiOk(await activityService.QueryAsync(projectId, query with { AggregateType = "ProjectUpdate" }, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementProjectUpdateRequest request, CancellationToken cancellationToken) =>
        ApiOk(await updateService.CreateAsync(projectId, request, cancellationToken));
}
