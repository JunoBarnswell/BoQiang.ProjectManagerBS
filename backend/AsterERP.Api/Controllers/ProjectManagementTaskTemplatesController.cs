using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/task-templates")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTaskTemplatesController(IProjectManagementTaskTemplateService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));
    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementTaskAdd)]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementTaskTemplateUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));
    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    public async Task<IActionResult> UpdateAsync(string projectId, string id, [FromBody] ProjectManagementTaskTemplateUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(projectId, id, request, cancellationToken));
    [HttpPost("{id}/apply")]
    [Permission(PermissionCodes.ProjectManagementTaskAdd)]
    public async Task<IActionResult> ApplyAsync(string projectId, string id, [FromBody] ProjectManagementTaskTemplateApplyRequest request, CancellationToken cancellationToken) => ApiOk(await service.ApplyAsync(id, request with { ProjectId = projectId }, cancellationToken));
}
