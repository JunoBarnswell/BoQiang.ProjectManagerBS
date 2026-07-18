using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/members")]
[Permission(PermissionCodes.ProjectManagementMemberView)]
public sealed class ProjectManagementMembersController(IProjectManagementMemberService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementMemberManage)]
    public async Task<IActionResult> AddAsync(string projectId, [FromBody] ProjectManagementMemberUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.AddAsync(projectId, request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementMemberManage)]
    public async Task<IActionResult> UpdateAsync(string projectId, string id, [FromBody] ProjectManagementMemberUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(projectId, id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementMemberManage)]
    public async Task<IActionResult> RemoveAsync(string projectId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.RemoveAsync(projectId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }
}
