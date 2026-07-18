using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/labels")]
[Permission(PermissionCodes.ProjectManagementLabelView)]
public sealed class ProjectManagementLabelsController(IProjectManagementLabelService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementLabelManage)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementLabelManage)]
    public async Task<IActionResult> UpdateAsync(string projectId, string id, [FromBody] ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(projectId, id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementLabelManage)]
    public async Task<IActionResult> DeleteAsync(string projectId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }
}

[Route("api/project-management/tasks/{taskId}/labels")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTaskLabelsController(IProjectManagementLabelService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string taskId, CancellationToken cancellationToken) => ApiOk(await service.QueryTaskLabelsAsync(taskId, cancellationToken));

    [HttpPut]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    public async Task<IActionResult> SetAsync(string taskId, [FromBody] ProjectManagementTaskLabelSetRequest request, CancellationToken cancellationToken)
    {
        await service.SetTaskLabelsAsync(taskId, request, cancellationToken);
        return ApiOk(new { taskId });
    }
}
