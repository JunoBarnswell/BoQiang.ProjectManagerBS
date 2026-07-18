using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/tasks/{taskId}/attachments")]
[Permission(PermissionCodes.ProjectManagementCommentView)]
public sealed class ProjectManagementTaskAttachmentsController(IProjectManagementTaskAttachmentService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string taskId, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(taskId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementAttachmentManage)]
    public async Task<IActionResult> UploadAsync(string taskId, IFormFile file, CancellationToken cancellationToken) => ApiOk(await service.UploadAsync(taskId, file, cancellationToken));

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadAsync(string taskId, string id, CancellationToken cancellationToken)
    {
        var result = await service.DownloadAsync(taskId, id, cancellationToken);
        return File(result.Stream, result.Metadata.ContentType, result.Metadata.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementAttachmentManage)]
    public async Task<IActionResult> DeleteAsync(string taskId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(taskId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }
}
