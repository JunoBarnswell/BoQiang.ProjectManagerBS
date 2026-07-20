using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/subscription")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementProjectSubscriptionsController(IProjectManagementProjectSubscriptionService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPut]
    public async Task<IActionResult> SaveAsync(string projectId, [FromBody] ProjectManagementProjectSubscriptionUpsertRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await service.SaveAsync(projectId, request, cancellationToken));
        }
        catch (ProjectManagementProjectSubscriptionVersionConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Ok(exception.Conflict, HttpContext.TraceIdentifier, exception.Message));
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsync(string projectId, [FromQuery] long? versionNo, CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteAsync(projectId, versionNo, cancellationToken);
            return ApiOk(new { projectId });
        }
        catch (ProjectManagementProjectSubscriptionVersionConflictException exception)
        {
            return StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Ok(exception.Conflict, HttpContext.TraceIdentifier, exception.Message));
        }
    }
}
