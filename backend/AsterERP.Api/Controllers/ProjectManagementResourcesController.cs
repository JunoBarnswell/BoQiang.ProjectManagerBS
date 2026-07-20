using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/resources")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementResourcesController(IProjectManagementResourceService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) =>
        ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpPatch("{id}")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> UpdateAsync(string projectId, string id, [FromBody] ProjectManagementResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateAsync(projectId, id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> DeleteAsync(string projectId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }
}
