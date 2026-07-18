using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/overview")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementOverviewController(IProjectManagementOverviewService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync([FromQuery] ProjectManagementOverviewQuery query, CancellationToken cancellationToken) =>
        ApiOk(await service.QueryAsync(query, cancellationToken));
}
