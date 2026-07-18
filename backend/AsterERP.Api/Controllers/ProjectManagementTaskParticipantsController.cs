using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/tasks/{taskId}/participants")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTaskParticipantsController(IProjectManagementTaskParticipantService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string taskId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(taskId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementTaskAssign)]
    public async Task<IActionResult> AddAsync(string taskId, [FromBody] ProjectManagementTaskParticipantUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.AddAsync(taskId, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementTaskAssign)]
    public async Task<IActionResult> RemoveAsync(string taskId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.RemoveAsync(taskId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }
}
