using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/projects/{projectId}/im-conversation")]
[Permission(PermissionCodes.ProjectManagementImConversationView)]
public sealed class ProjectManagementImConversationsController(IProjectManagementImConversationService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(string projectId, [FromQuery] string? taskId, CancellationToken cancellationToken) =>
        ApiOk(await service.GetAsync(projectId, taskId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementImConversationManage)]
    public async Task<IActionResult> EnsureAsync(string projectId, [FromBody] ProjectManagementImConversationEnsureRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.EnsureAsync(projectId, request, cancellationToken));

}
