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

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadAsync(string id, CancellationToken cancellationToken)
    {
        var download = await service.DownloadAsync(id, cancellationToken);
        return File(download.Stream, download.ContentType, download.FileName);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ProjectManagementBackupRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(request, cancellationToken));

    [HttpGet("{id}/restore-preview")]
    public async Task<IActionResult> PreviewRestoreAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.PreviewRestoreAsync(id, cancellationToken));

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreAsync(string id, [FromBody] ProjectManagementRestoreRequest request, CancellationToken cancellationToken) => ApiOk(await service.RestoreAsync(id, request, cancellationToken));

    [HttpPost("{id}/delete")]
    public async Task<IActionResult> DeleteAsync(string id, [FromBody] ProjectManagementBackupDeleteRequest request, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, request, cancellationToken);
        return ApiOk(true);
    }
}
