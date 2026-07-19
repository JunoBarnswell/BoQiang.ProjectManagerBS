using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/home")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementHomeController(IProjectManagementHomeQueryService service) : BaseApiController
{
    [HttpGet("projects")]
    public async Task<IActionResult> ProjectsAsync([FromQuery] ProjectManagementHomeQuery query, CancellationToken cancellationToken) =>
        ApiOk(await service.QueryProjectsAsync(query, cancellationToken));

    [HttpGet("summary")]
    public async Task<IActionResult> SummaryAsync([FromQuery] ProjectManagementHomeQuery query, CancellationToken cancellationToken) =>
        ApiOk(await service.QuerySummaryAsync(query, cancellationToken));
}
