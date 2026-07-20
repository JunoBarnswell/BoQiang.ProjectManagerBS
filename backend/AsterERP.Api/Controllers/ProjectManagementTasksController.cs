using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[ApiController]
[Route("api/project-management")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTasksController(IProjectManagementTaskService service) : BaseApiController
{
    [HttpGet("projects/{projectId}/work-items")]
    public async Task<IActionResult> QueryAsync(string projectId, [FromQuery] ProjectManagementTaskQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.QueryAsync(query with { ProjectId = projectId }, cancellationToken));

    [HttpGet("work-items/{id}")]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPost("projects/{projectId}/work-items")]
    [Permission(PermissionCodes.ProjectManagementTaskAdd)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpPut("work-items/{id}")]
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

    [HttpPost("work-items/{id}/status")]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    public async Task<IActionResult> ChangeStatusAsync(string id, [FromBody] ProjectManagementTaskStatusChangeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await service.ChangeStatusAsync(id, request, cancellationToken));
        }
        catch (ProjectManagementTaskVersionConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Ok(exception.Conflict, HttpContext.TraceIdentifier, exception.Message));
        }
    }

    [HttpPost("work-items/{id}/force-start")]
    [Permission(PermissionCodes.ProjectManagementTaskManageDependency)]
    public async Task<IActionResult> ForceStartAsync(string id, [FromBody] ProjectManagementTaskDependencyForceStartRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.ForceStartAsync(id, request, cancellationToken));

    [HttpPost("work-items/{id}/move")]
    [Permission(PermissionCodes.ProjectManagementTaskMove)]
    public async Task<IActionResult> MoveAsync(string id, [FromBody] ProjectManagementTaskMoveRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.MoveAsync(id, request, cancellationToken));

    [HttpDelete("work-items/{id}")]
    [Permission(PermissionCodes.ProjectManagementTaskDelete)]
    public async Task<IActionResult> DeleteAsync(string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }

    [HttpPost("work-items/{id}/delete")]
    [Permission(PermissionCodes.ProjectManagementTaskDelete)]
    public async Task<IActionResult> DeleteWithPolicyAsync(string id, [FromBody] ProjectManagementTaskDeleteRequest request, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, request, cancellationToken);
        return ApiOk(new { id, request.Mode });
    }

    [HttpPost("work-items/{id}/restore")]
    [Permission(PermissionCodes.ProjectManagementTaskRestore)]
    public async Task<IActionResult> RestoreAsync(string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
        => ApiOk(await service.RestoreAsync(id, versionNo, cancellationToken));
}
