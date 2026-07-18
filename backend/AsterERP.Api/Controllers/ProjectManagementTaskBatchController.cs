using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/tasks/batch")]
[Permission(PermissionCodes.ProjectManagementTaskEdit)]
public sealed class ProjectManagementTaskBatchController(IProjectManagementTaskBatchService service) : BaseApiController
{
    [HttpPost("update")]
    public async Task<IActionResult> UpdateAsync([FromBody] ProjectManagementTaskBatchUpdateRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(request, cancellationToken));
}
