using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/gantt-schedule")]
[Permission(PermissionCodes.ProjectManagementTaskEdit)]
public sealed class ProjectManagementGanttScheduleController(IProjectManagementGanttScheduleService service) : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> UpdateAsync(string projectId, [FromBody] ProjectManagementGanttScheduleBatchUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(projectId, request.ProjectId, StringComparison.Ordinal)) return BadRequest("路由项目与请求项目不一致");
        return ApiOk(await service.UpdateAsync(request, cancellationToken));
    }
}
