using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/task-templates")]
[Permission(PermissionCodes.ProjectManagementTaskTemplateManage)]
public sealed class ProjectManagementTaskTemplatesController(IProjectManagementTaskTemplateService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string projectId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(projectId, cancellationToken));
    [HttpPost]
    public async Task<IActionResult> CreateAsync(string projectId, [FromBody] ProjectManagementTaskTemplateUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(projectId, request, cancellationToken));
    [HttpPost("from-task")]
    public async Task<IActionResult> CreateFromTaskAsync(string projectId, [FromBody] ProjectManagementTaskTemplateCreateFromTaskRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateFromTaskAsync(projectId, request, cancellationToken));
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsync(string projectId, string id, [FromBody] ProjectManagementTaskTemplateUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(projectId, id, request, cancellationToken));
    [HttpPost("{id}/instantiate")]
    public async Task<IActionResult> InstantiateAsync(string projectId, string id, [FromBody] ProjectManagementTaskTemplateInstantiateRequest request, CancellationToken cancellationToken) => ApiOk(await service.InstantiateAsync(id, request with { ProjectId = projectId }, cancellationToken));
    [HttpPost("{id}/apply")]
    public async Task<IActionResult> ApplyAsync(string projectId, string id, [FromBody] ProjectManagementTaskTemplateApplyRequest request, CancellationToken cancellationToken) => ApiOk(await service.ApplyAsync(id, request with { ProjectId = projectId }, cancellationToken));
}
