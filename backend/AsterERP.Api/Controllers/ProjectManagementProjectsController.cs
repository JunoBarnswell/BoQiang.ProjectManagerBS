using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementProjectsController(IProjectManagementProjectService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync([FromQuery] ProjectManagementProjectQuery query, CancellationToken cancellationToken) =>
        ApiOk(await service.QueryAsync(query, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementProjectAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] ProjectManagementProjectUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] ProjectManagementProjectUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await service.UpdateAsync(id, request, cancellationToken));
        }
        catch (ProjectManagementProjectVersionConflictException exception)
        {
            return ApiConflict(exception);
        }
    }

    [HttpPost("{id}/archive")]
    [Permission(PermissionCodes.ProjectManagementProjectArchive)]
    public async Task<IActionResult> ArchiveAsync(string id, [FromBody] ProjectManagementProjectArchiveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await service.ArchiveAsync(id, request, cancellationToken));
        }
        catch (ProjectManagementProjectVersionConflictException exception)
        {
            return ApiConflict(exception);
        }
    }

    [HttpPost("{id}/restore")]
    [Permission(PermissionCodes.ProjectManagementProjectRestore)]
    public async Task<IActionResult> RestoreAsync(string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await service.RestoreAsync(id, versionNo, cancellationToken));
        }
        catch (ProjectManagementProjectVersionConflictException exception)
        {
            return ApiConflict(exception);
        }
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementProjectDelete)]
    public async Task<IActionResult> DeleteAsync(string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteAsync(id, versionNo, cancellationToken);
            return ApiOk(new { id });
        }
        catch (ProjectManagementProjectVersionConflictException exception)
        {
            return ApiConflict(exception);
        }
    }

    private IActionResult ApiConflict(ProjectManagementProjectVersionConflictException exception) =>
        StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Ok(exception.Conflict, HttpContext.TraceIdentifier, exception.Message));
}
