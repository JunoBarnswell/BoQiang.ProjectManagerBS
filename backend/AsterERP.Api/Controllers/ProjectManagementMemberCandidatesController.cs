using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/member-candidates")]
public sealed class ProjectManagementMemberCandidatesController(
    IProjectManagementMemberCandidateService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.ProjectManagementProjectView)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] ProjectManagementMemberCandidateQuery query,
        CancellationToken cancellationToken)
    {
        return ApiOk(await service.QueryAsync(query, cancellationToken));
    }
}
