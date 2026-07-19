using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[ApiController]
[Route("api/project-management/my-work")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementMyWorkController(IProjectManagementMyWorkService service) : BaseApiController
{
    [HttpGet("project-options")]
    public async Task<IActionResult> QueryProjectOptionsAsync([FromQuery] ProjectManagementMyWorkProjectOptionQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.QueryProjectOptionsAsync(query, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> QueryAsync([FromQuery] ProjectManagementMyWorkQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.QueryAsync(query, cancellationToken));
}
