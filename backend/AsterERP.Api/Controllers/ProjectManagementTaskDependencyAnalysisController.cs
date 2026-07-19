using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/task-dependency-analysis")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTaskDependencyAnalysisController(IProjectManagementTaskDependencyAnalysisService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> AnalyzeAsync(string projectId, CancellationToken cancellationToken) =>
        ApiOk(await service.AnalyzeAsync(projectId, cancellationToken));

    [HttpPost("impact-preview")]
    public async Task<IActionResult> PreviewImpactAsync(string projectId, [FromBody] ProjectManagementTaskDependencyImpactPreviewRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.PreviewImpactAsync(projectId, request, cancellationToken));
}
