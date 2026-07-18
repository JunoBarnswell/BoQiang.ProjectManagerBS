using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/operations")]
[Permission(PermissionCodes.ProjectManagementOperationView)]
public sealed class ProjectManagementOperationsController(IProjectManagementOperationService service) : BaseApiController
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPost("maintenance/workspace-validation")]
    [Permission(PermissionCodes.ProjectManagementOperationManage)]
    public async Task<IActionResult> ValidateWorkspaceAsync(CancellationToken cancellationToken) => ApiOk(await service.RunWorkspaceValidationAsync(cancellationToken));

    [HttpPost("{id}/cancel")]
    [Permission(PermissionCodes.ProjectManagementOperationManage)]
    public async Task<IActionResult> CancelAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.RequestCancellationAsync(id, cancellationToken));
}
