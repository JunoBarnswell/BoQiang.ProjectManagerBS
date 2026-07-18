using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/reversible-commands")]
[Permission(PermissionCodes.ProjectManagementReversibleCommandView)]
public sealed class ProjectManagementReversibleCommandsController(IProjectManagementReversibleCommandService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetStackAsync(CancellationToken cancellationToken) => ApiOk(await service.GetStackAsync(cancellationToken));

    [HttpPost("undo")]
    [Permission(PermissionCodes.ProjectManagementReversibleCommandManage)]
    public async Task<IActionResult> UndoAsync([FromBody] ProjectManagementReversibleCommandExecuteRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.UndoAsync(request, cancellationToken));

    [HttpPost("redo")]
    [Permission(PermissionCodes.ProjectManagementReversibleCommandManage)]
    public async Task<IActionResult> RedoAsync([FromBody] ProjectManagementReversibleCommandExecuteRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.RedoAsync(request, cancellationToken));
}
