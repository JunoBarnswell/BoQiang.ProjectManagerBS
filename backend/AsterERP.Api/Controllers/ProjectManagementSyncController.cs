using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/sync")]
public sealed class ProjectManagementSyncController(IProjectManagementSyncService service) : BaseApiController
{
    private const long MaxSyncPackageBytes = 200L * 1024 * 1024;

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
        var validationError = ValidatePackageFile(file);
        if (validationError is not null) return validationError;
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
        var validationError = ValidatePackageFile(file);
        if (validationError is not null) return validationError;
        await using var stream = file.OpenReadStream();
        return ApiOk(await service.ImportAsync(stream, new ProjectManagementSyncImportRequest(currentPassword, confirmRisk, conflictStrategy, idempotencyKey, deviceId), cancellationToken));
    }

    [HttpGet("history")]
    [Permission(PermissionCodes.ProjectManagementSyncImport)]
    public async Task<IActionResult> HistoryAsync([FromQuery] ProjectManagementSyncHistoryQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.GetHistoryAsync(query, cancellationToken));

    [HttpGet("history/{id}")]
    [Permission(PermissionCodes.ProjectManagementSyncImport)]
    public async Task<IActionResult> HistoryDetailAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await service.GetHistoryDetailAsync(id, cancellationToken));

    [HttpGet("history/{id}/report")]
    [Permission(PermissionCodes.ProjectManagementSyncImport)]
    public async Task<IActionResult> DownloadHistoryReportAsync(string id, CancellationToken cancellationToken)
    {
        var result = await service.DownloadHistoryReportAsync(id, cancellationToken);
        return File(result.Content, "text/csv; charset=utf-8", result.FileName);
    }

    [HttpPost("history/{id}/retry")]
    [Permission(PermissionCodes.ProjectManagementSyncImport)]
    public async Task<IActionResult> RetryAsync(
        string id,
        IFormFile file,
        [FromForm] string currentPassword,
        [FromForm] bool confirmRisk,
        [FromForm] string conflictStrategy,
        [FromForm] string? idempotencyKey,
        [FromForm] string? deviceId,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePackageFile(file);
        if (validationError is not null) return validationError;
        await using var stream = file.OpenReadStream();
        return ApiOk(await service.RetryAsync(id, stream, new ProjectManagementSyncImportRequest(currentPassword, confirmRisk, conflictStrategy, idempotencyKey, deviceId), cancellationToken));
    }

    private static IActionResult? ValidatePackageFile(IFormFile? file)
    {
        if (file is null || file.Length <= 0) return new BadRequestObjectResult("同步包不能为空");
        if (file.Length > MaxSyncPackageBytes) return new BadRequestObjectResult("同步包超过 200 MB 限制");
        if (!string.Equals(Path.GetExtension(file.FileName), ".bqsync", StringComparison.OrdinalIgnoreCase))
            return new BadRequestObjectResult("只支持 .bqsync 同步包");
        return null;
    }
}
