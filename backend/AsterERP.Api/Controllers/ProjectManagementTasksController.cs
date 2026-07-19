using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[ApiController]
[Route("api/project-management/tasks")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTasksController(IProjectManagementTaskService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync([FromQuery] ProjectManagementTaskQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.QueryAsync(query, cancellationToken));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPost("{projectId}")]
    [Permission(PermissionCodes.ProjectManagementTaskAdd)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await service.UpdateAsync(id, request, cancellationToken));
        }
        catch (ProjectManagementTaskVersionConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Ok(exception.Conflict, HttpContext.TraceIdentifier, exception.Message));
        }
    }

    [HttpPost("{id}/force-start")]
    [Permission(PermissionCodes.ProjectManagementTaskManageDependency)]
    public async Task<IActionResult> ForceStartAsync(string id, [FromBody] ProjectManagementTaskDependencyForceStartRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.ForceStartAsync(id, request, cancellationToken));

    [HttpPost("{id}/move")]
    [Permission(PermissionCodes.ProjectManagementTaskMove)]
    public async Task<IActionResult> MoveAsync(string id, [FromBody] ProjectManagementTaskMoveRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.MoveAsync(id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementTaskDelete)]
    public async Task<IActionResult> DeleteAsync(string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }

    [HttpPost("{id}/delete")]
    [Permission(PermissionCodes.ProjectManagementTaskDelete)]
    public async Task<IActionResult> DeleteWithPolicyAsync(string id, [FromBody] ProjectManagementTaskDeleteRequest request, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, request, cancellationToken);
        return ApiOk(new { id, request.Mode });
    }

    [HttpPost("{id}/restore")]
    [Permission(PermissionCodes.ProjectManagementTaskRestore)]
    public async Task<IActionResult> RestoreAsync(string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
        => ApiOk(await service.RestoreAsync(id, versionNo, cancellationToken));
}
