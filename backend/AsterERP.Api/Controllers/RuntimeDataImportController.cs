using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.Runtime;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/runtime/models")]
public sealed class RuntimeDataImportController(RuntimeDataImportService importService) : BaseApiController
{
    [HttpGet("{modelCode}/import-template")]
    public async Task<IActionResult> GetImportTemplateAsync(
        string modelCode,
        [FromQuery] string pageCode,
        CancellationToken cancellationToken)
    {
        var (content, fileName) = await importService.BuildImportTemplateAsync(modelCode, pageCode, cancellationToken);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost("{modelCode}/import-preview")]
    public async Task<IActionResult> PreviewAsync(
        string modelCode,
        [FromForm] string pageCode,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return ApiOk(await importService.PreviewAsync(modelCode, pageCode, stream, cancellationToken));
    }

    [HttpPost("{modelCode}/import")]
    public async Task<IActionResult> ImportAsync(
        string modelCode,
        [FromForm] string pageCode,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return ApiOk(await importService.ImportAsync(modelCode, pageCode, stream, cancellationToken));
    }

    [HttpPost("{modelCode}/export")]
    public async Task<IActionResult> ExportAsync(
        string modelCode,
        [FromBody] RuntimeExportRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await importService.ExportAsync(modelCode, request, cancellationToken));
    }
}
