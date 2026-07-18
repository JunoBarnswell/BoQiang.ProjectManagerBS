using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/recycle")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementRecycleController(IProjectManagementRecycleService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync([FromQuery] ProjectManagementRecycleQuery query, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(query, cancellationToken));

    [HttpPost("projects/{id}/restore")]
    [Permission(PermissionCodes.ProjectManagementProjectRestore)]
    public async Task<IActionResult> RestoreProjectAsync(string id, [FromBody] ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken) { await service.RestoreProjectAsync(id, request, cancellationToken); return ApiOk(new { id }); }

    [HttpPost("tasks/{id}/restore")]
    [Permission(PermissionCodes.ProjectManagementTaskRestore)]
    public async Task<IActionResult> RestoreTaskAsync(string id, [FromBody] ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken) { await service.RestoreTaskAsync(id, request, cancellationToken); return ApiOk(new { id }); }

    [HttpGet("tasks/{id}/purge-preview")]
    [Permission(PermissionCodes.ProjectManagementTaskDelete)]
    public async Task<IActionResult> PreviewPurgeTaskAsync(string id, [FromQuery] long versionNo, [FromQuery] bool purgeDescendants, CancellationToken cancellationToken) => ApiOk(await service.PreviewPurgeTaskAsync(id, versionNo, purgeDescendants, cancellationToken));

    [HttpPost("tasks/{id}/purge")]
    [Permission(PermissionCodes.ProjectManagementTaskDelete)]
    public async Task<IActionResult> PurgeTaskAsync(string id, [FromBody] ProjectManagementRecycleTaskPurgeRequest request, CancellationToken cancellationToken) { await service.PurgeTaskAsync(id, request, cancellationToken); return ApiOk(new { id }); }

    [HttpGet("projects/{id}/purge-preview")]
    [Permission(PermissionCodes.ProjectManagementProjectPurge)]
    public async Task<IActionResult> PreviewPurgeProjectAsync(string id, [FromQuery] long versionNo, CancellationToken cancellationToken) => ApiOk(await service.PreviewPurgeProjectAsync(id, versionNo, cancellationToken));

    [HttpPost("projects/{id}/purge")]
    [Permission(PermissionCodes.ProjectManagementProjectPurge)]
    public async Task<IActionResult> PurgeProjectAsync(string id, [FromBody] ProjectManagementRecyclePurgeRequest request, CancellationToken cancellationToken) { await service.PurgeProjectAsync(id, request, cancellationToken); return ApiOk(new { id }); }
}
