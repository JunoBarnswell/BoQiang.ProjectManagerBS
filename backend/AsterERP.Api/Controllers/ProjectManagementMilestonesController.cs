using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/milestones")]
[Permission(PermissionCodes.ProjectManagementMilestoneView)]
public sealed class ProjectManagementMilestonesController(IProjectManagementMilestoneService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementMilestoneManage)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementMilestoneUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementMilestoneManage)]
    public async Task<IActionResult> UpdateAsync(string projectId, string id, [FromBody] ProjectManagementMilestoneUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(projectId, id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementMilestoneManage)]
    public async Task<IActionResult> DeleteAsync(string projectId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }
}
