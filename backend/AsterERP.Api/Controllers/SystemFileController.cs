using AsterERP.Api.Application.System.Files;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/files")]
public sealed class SystemFileController(IFileAppService fileAppService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.SystemFileQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await fileAppService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("{id}")]
    [Permission(PermissionCodes.SystemFileQuery)]
    public async Task<IActionResult> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await fileAppService.GetDetailAsync(id, cancellationToken));
    }

    [HttpGet("formats")]
    [Permission(PermissionCodes.SystemFileQuery)]
    public async Task<IActionResult> GetPreviewFormatsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await fileAppService.GetPreviewFormatsAsync(cancellationToken));
    }

    [HttpPost("upload")]
    [Permission(PermissionCodes.SystemFileUpload)]
    public async Task<IActionResult> UploadAsync([FromForm] IFormFile file, [FromForm] string? remark, CancellationToken cancellationToken)
    {
        return ApiOk(await fileAppService.UploadAsync(file, remark, cancellationToken));
    }

    [HttpGet("{id}/preview")]
    [Permission(PermissionCodes.SystemFilePreview)]
    public async Task<IActionResult> PreviewAsync(string id, CancellationToken cancellationToken)
    {
        var result = await fileAppService.PreviewAsync(id, cancellationToken);
        Response.Headers.ContentDisposition = BuildInlineContentDisposition(result.FileName);
        Response.Headers.AcceptRanges = "bytes";
        return new FileStreamResult(result.Stream, result.ContentType)
        {
            EnableRangeProcessing = true
        };
    }

    [HttpGet("{id}/download")]
    [Permission(PermissionCodes.SystemFileDownload)]
    public async Task<IActionResult> DownloadAsync(string id, CancellationToken cancellationToken)
    {
        var result = await fileAppService.DownloadAsync(id, cancellationToken);
        return File(result.Stream, result.Metadata.ContentType, result.Metadata.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.SystemFileDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await fileAppService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    private static string BuildInlineContentDisposition(string fileName)
    {
        var asciiFileName = BuildAsciiFileNameFallback(fileName);
        var encodedFileName = Uri.EscapeDataString(fileName);
        return $"inline; filename=\"{asciiFileName}\"; filename*=UTF-8''{encodedFileName}";
    }

    private static string BuildAsciiFileNameFallback(string fileName)
    {
        var fallback = string.Create(fileName.Length, fileName, static (buffer, value) =>
        {
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                buffer[index] = current is >= (char)0x20 and <= (char)0x7E && current != '"' && current != '\\'
                    ? current
                    : '_';
            }
        }).Trim();

        return string.IsNullOrWhiteSpace(fallback) ? "preview" : fallback;
    }
}
