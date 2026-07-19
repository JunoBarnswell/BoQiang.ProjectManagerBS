using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/excel-import")]
[Permission(PermissionCodes.ProjectManagementSyncImport)]
public sealed class ProjectManagementExcelImportController(
    IProjectManagementExcelImportService service,
    IProjectManagementExcelImportConfirmService confirmService) : BaseApiController
{
    [HttpGet("template")]
    public async Task<IActionResult> DownloadTemplateAsync(CancellationToken cancellationToken)
    {
        var file = await service.DownloadTemplateAsync(cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0) return BadRequest("Excel 文件不能为空");
        return ApiOk(await service.PreviewAsync(file, cancellationToken));
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmAsync(
        [FromForm] string previewId,
        [FromForm] string idempotencyKey,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0) return BadRequest("Excel 文件不能为空");
        return ApiOk(await confirmService.ConfirmAsync(new ProjectManagementExcelImportConfirmRequest(previewId, idempotencyKey), file, cancellationToken));
    }

    [HttpGet("results/{importId}")]
    public async Task<IActionResult> GetResultAsync(string importId, CancellationToken cancellationToken) =>
        ApiOk(await confirmService.GetResultAsync(importId, cancellationToken));
}
