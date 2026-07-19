using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/sync")]
public sealed class ProjectManagementSyncController(IProjectManagementSyncService service) : BaseApiController
{
    [HttpGet("watermark")]
    [Permission(PermissionCodes.ProjectManagementSyncExport)]
    public async Task<IActionResult> WatermarkAsync([FromQuery] string deviceId, CancellationToken cancellationToken)
        => ApiOk(await service.GetWatermarkAsync(deviceId, cancellationToken));

    [HttpGet("changes")]
    [Permission(PermissionCodes.ProjectManagementSyncExport)]
    public async Task<IActionResult> ChangesAsync([FromQuery] string? projectId, [FromQuery] long sinceSequenceNo = 0, [FromQuery] int limit = 200, CancellationToken cancellationToken = default)
        => ApiOk(await service.GetChangesAsync(projectId, sinceSequenceNo, limit, cancellationToken));

    [HttpPost("acknowledge")]
    [Permission(PermissionCodes.ProjectManagementSyncImport)]
    public async Task<IActionResult> AcknowledgeAsync([FromBody] ProjectManagementSyncAcknowledgeRequest request, CancellationToken cancellationToken)
        => ApiOk(await service.AcknowledgeAsync(request, cancellationToken));

    [HttpPost("export")]
    [Permission(PermissionCodes.ProjectManagementSyncExport)]
    public async Task<IActionResult> ExportAsync([FromBody] ProjectManagementSyncExportRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ExportAsync(request, cancellationToken);
        return File(result.Content, "application/octet-stream", result.FileName);
    }

    [HttpPost("preview")]
    [Permission(PermissionCodes.ProjectManagementSyncImport)]
    public async Task<IActionResult> PreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0) return BadRequest("同步包不能为空");
        await using var stream = file.OpenReadStream();
        return ApiOk(await service.PreviewAsync(stream, cancellationToken));
    }

    [HttpPost("apply")]
    [Permission(PermissionCodes.ProjectManagementSyncImport)]
    public async Task<IActionResult> ApplyAsync(
        IFormFile file,
        [FromForm] string currentPassword,
        [FromForm] bool confirmRisk,
        [FromForm] string conflictStrategy,
        [FromForm] string? idempotencyKey,
        [FromForm] string? deviceId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0) return BadRequest("同步包不能为空");
        await using var stream = file.OpenReadStream();
        return ApiOk(await service.ImportAsync(stream, new ProjectManagementSyncImportRequest(currentPassword, confirmRisk, conflictStrategy, idempotencyKey, deviceId), cancellationToken));
    }
}
