using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/tasks/{taskId}/attachments")]
public sealed class ProjectManagementTaskAttachmentsController(IProjectManagementTaskAttachmentService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.ProjectManagementTaskView)]
    public async Task<IActionResult> QueryAsync(string taskId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(taskId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementAttachmentManage)]
    public async Task<IActionResult> UploadAsync(string taskId, IFormFile file, CancellationToken cancellationToken) => ApiOk(await service.UploadAsync(taskId, file, cancellationToken));

    [HttpGet("{id}/download")]
    [Permission(PermissionCodes.ProjectManagementTaskView)]
    public async Task<IActionResult> DownloadAsync(string taskId, string id, CancellationToken cancellationToken)
    {
        var result = await service.DownloadAsync(taskId, id, cancellationToken);
        return File(result.Stream, result.Metadata.ContentType, result.Metadata.FileName, enableRangeProcessing: true);
    }

    [HttpGet("{id}/preview")]
    [Permission(PermissionCodes.ProjectManagementTaskView)]
    public async Task<IActionResult> PreviewAsync(string taskId, string id, CancellationToken cancellationToken)
    {
        var result = await service.PreviewAsync(taskId, id, cancellationToken);
        Response.Headers.ContentDisposition = BuildInlineContentDisposition(result.Preview.FileName);
        Response.Headers.AcceptRanges = "bytes";
        return new FileStreamResult(result.Preview.Stream, result.Preview.ContentType)
        {
            EnableRangeProcessing = true
        };
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementAttachmentManage)]
    public async Task<IActionResult> DeleteAsync(string taskId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(taskId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }

    private static string BuildInlineContentDisposition(string fileName)
    {
        var fallback = string.Create(fileName.Length, fileName, static (buffer, value) =>
        {
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                buffer[index] = current is >= (char)0x20 and <= (char)0x7E && current != '"' && current != '\\' ? current : '_';
            }
        }).Trim();
        return $"inline; filename=\"{(string.IsNullOrWhiteSpace(fallback) ? "preview" : fallback)}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}
