using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/tasks/{taskId}/comments")]
[Permission(PermissionCodes.ProjectManagementCommentView)]
public sealed class ProjectManagementTaskCommentsController(IProjectManagementTaskCommentService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync(string taskId, [FromQuery] ProjectManagementTaskCommentQuery query, CancellationToken cancellationToken) => ApiOk(await service.QueryAsync(taskId, query, cancellationToken));

    [HttpGet("mention-candidates")]
    public async Task<IActionResult> QueryMentionCandidatesAsync(string taskId, [FromQuery] ProjectManagementTaskCommentMentionCandidateQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.QueryMentionCandidatesAsync(taskId, query, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.ProjectManagementCommentAdd)]
    public async Task<IActionResult> CreateAsync(string taskId, [FromBody] ProjectManagementTaskCommentUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(taskId, request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.ProjectManagementCommentAdd)]
    public async Task<IActionResult> UpdateAsync(string taskId, string id, [FromBody] ProjectManagementTaskCommentUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(taskId, id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.ProjectManagementCommentAdd)]
    public async Task<IActionResult> DeleteAsync(string taskId, string id, [FromQuery] long versionNo, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(taskId, id, versionNo, cancellationToken);
        return ApiOk(new { id });
    }
}
