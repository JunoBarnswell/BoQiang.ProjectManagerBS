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
    public async Task<IActionResult> QueryAsync(string projectId, [FromQuery] int limit = 100, CancellationToken cancellationToken = default) => ApiOk(await service.QueryAsync(projectId, limit, cancellationToken));
}
