using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/tasks/{taskId}/reminders")]
[Permission(PermissionCodes.ProjectManagementReminderView)]
public sealed class ProjectManagementTaskRemindersController(IProjectManagementTaskReminderService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string taskId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(taskId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementReminderManage)]
    public async Task<IActionResult> CreateAsync(string taskId, [FromBody] ProjectManagementTaskReminderCreateRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(taskId, request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementReminderManage)]
    public async Task<IActionResult> UpdateAsync(string taskId, string id, [FromBody] ProjectManagementTaskReminderUpdateRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(taskId, id, request, cancellationToken));

    [HttpPost("{id}/cancel")]
    [Permission(PermissionCodes.ProjectManagementReminderManage)]
    public async Task<IActionResult> CancelAsync(string taskId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken) { await service.CancelAsync(taskId, id, versionNo, cancellationToken); return ApiOk(new { id }); }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementReminderManage)]
    public async Task<IActionResult> DeleteAsync(string taskId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken) { await service.DeleteAsync(taskId, id, versionNo, cancellationToken); return ApiOk(new { id }); }
}
