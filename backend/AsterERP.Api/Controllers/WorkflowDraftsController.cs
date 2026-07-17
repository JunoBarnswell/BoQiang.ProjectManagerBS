using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/drafts")]
public sealed class WorkflowDraftsController(IWorkflowRequestDraftAppService workflowDraftService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowDraftQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowDraftService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.WorkflowDraftEdit)]
    public async Task<IActionResult> SaveAsync([FromBody] WorkflowRequestDraftUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowDraftService.SaveAsync(request, cancellationToken));
    }

    [HttpPost("{id}/submit")]
    [Permission(PermissionCodes.WorkflowDraftSubmit)]
    public async Task<IActionResult> SubmitAsync(string id, [FromBody] WorkflowRequestDraftSubmitRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowDraftService.SubmitAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.WorkflowDraftDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await workflowDraftService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }
}
