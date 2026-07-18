using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/search")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementSearchController(IProjectManagementSearchService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] ProjectManagementSearchQuery query, CancellationToken cancellationToken) => ApiOk(await service.SearchAsync(query, cancellationToken));
}
