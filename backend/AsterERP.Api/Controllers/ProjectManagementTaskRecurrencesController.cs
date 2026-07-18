using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/task-recurrences")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTaskRecurrencesController(IProjectManagementTaskRecurrenceService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementTaskAdd)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementTaskRecurrenceCreateRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(string projectId, string id, CancellationToken cancellationToken) => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    public async Task<IActionResult> UpdateAsync(string projectId, string id, [FromBody] ProjectManagementTaskRecurrenceUpdateRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(id, request, cancellationToken));

    [HttpGet("{id}/occurrences")]
    public async Task<IActionResult> QueryOccurrencesAsync(string projectId, string id, CancellationToken cancellationToken) => ApiOk(await service.QueryOccurrencesAsync(id, cancellationToken));

    [HttpPut("{id}/occurrences/{occurrenceId}")]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    public async Task<IActionResult> EditOccurrenceAsync(string projectId, string id, string occurrenceId, [FromBody] ProjectManagementTaskRecurrenceOccurrenceEditRequest request, CancellationToken cancellationToken)
    {
        await service.EditOccurrenceAsync(id, occurrenceId, request, cancellationToken);
        return ApiOk(new { id = occurrenceId });
    }

    [HttpDelete("{id}/occurrences/{occurrenceId}")]
    [Permission(PermissionCodes.ProjectManagementTaskDelete)]
    public async Task<IActionResult> DeleteOccurrenceAsync(string projectId, string id, string occurrenceId, [FromBody] ProjectManagementTaskRecurrenceOccurrenceDeleteRequest request, CancellationToken cancellationToken)
    {
        await service.DeleteOccurrenceAsync(id, occurrenceId, request, cancellationToken);
        return ApiOk(new { id = occurrenceId });
    }
}
