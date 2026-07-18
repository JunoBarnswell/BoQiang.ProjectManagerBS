using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/backups")]
[Permission(PermissionCodes.ProjectManagementBackupManage)]
public sealed class ProjectManagementBackupController(IProjectManagementBackupService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken) => ApiOk(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ProjectManagementBackupRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(request, cancellationToken));

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreAsync(string id, [FromBody] ProjectManagementRestoreRequest request, CancellationToken cancellationToken) => ApiOk(await service.RestoreAsync(id, request, cancellationToken));
}
