using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/work-items/{taskId}/followers")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTaskFollowersController(IProjectManagementTaskFollowerService service) : BaseApiController
{
    [HttpGet] public async Task<IActionResult> QueryAsync(string taskId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(taskId, cancellationToken));
    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    public async Task<IActionResult> AddAsync(string taskId, [FromBody] ProjectManagementTaskFollowerUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.AddAsync(taskId, request, cancellationToken));
    [HttpDelete("{userId}")]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    public async Task<IActionResult> RemoveAsync(string taskId, string userId, [FromQuery] long versionNo, CancellationToken cancellationToken) { await service.RemoveAsync(taskId, userId, versionNo, cancellationToken); return ApiOk(new { userId }); }
}
