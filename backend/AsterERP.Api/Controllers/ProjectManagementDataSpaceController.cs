using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/data-space")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementDataSpaceController(IProjectManagementDataSpaceService service) : BaseApiController
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken) => ApiOk(await service.GetSummaryAsync(cancellationToken));
}
