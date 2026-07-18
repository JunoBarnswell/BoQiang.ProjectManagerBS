using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/task-dependencies")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTaskDependenciesController(IProjectManagementTaskDependencyService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementTaskManageDependency)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementTaskDependencyUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpPost("batch")]
    [Permission(PermissionCodes.ProjectManagementTaskManageDependency)]
    public async Task<IActionResult> CreateBatchAsync(string projectId, [FromBody] ProjectManagementTaskDependencyBatchCreateRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateBatchAsync(projectId, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementTaskManageDependency)]
    public async Task<IActionResult> DeleteAsync(string projectId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }
}
