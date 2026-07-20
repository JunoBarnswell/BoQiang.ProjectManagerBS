using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/saved-views")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementSavedViewsController(IProjectManagementSavedViewService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementSavedViewUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> UpdateAsync(string projectId, string id, [FromBody] ProjectManagementSavedViewUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(projectId, id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> DeleteAsync(string projectId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken) { await service.DeleteAsync(projectId, id, versionNo, cancellationToken); return ApiOk(new { id }); }
}
