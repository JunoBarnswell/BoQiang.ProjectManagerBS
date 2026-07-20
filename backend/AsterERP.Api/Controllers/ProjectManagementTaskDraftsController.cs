using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/drafts")]
[Permission(PermissionCodes.ProjectManagementTaskView)]
public sealed class ProjectManagementTaskDraftsController(IProjectManagementTaskDraftService service) : BaseApiController
{
    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementTaskAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] ProjectManagementTaskDraftCreateRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(request, cancellationToken));
    [HttpGet("{id}")] public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetAsync(id, cancellationToken));
    [HttpPost("{id}/attachments")]
    [Permission(PermissionCodes.ProjectManagementAttachmentManage)]
    public async Task<IActionResult> UploadAsync(string id, IFormFile file, CancellationToken cancellationToken) => ApiOk(await service.UploadAsync(id, file, cancellationToken));
    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementTaskAdd)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken) { await service.DeleteAsync(id, cancellationToken); return ApiOk(new { id }); }
}
