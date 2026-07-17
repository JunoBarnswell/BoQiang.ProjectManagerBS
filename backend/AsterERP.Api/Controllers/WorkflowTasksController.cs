using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/tasks")]
public sealed class WorkflowTasksController(IWorkflowTaskAppService workflowTaskService) : BaseApiController
{
    [HttpGet("summary")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetSummaryAsync(cancellationToken));
    }

    [HttpGet("todo")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetTodoAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetTodoAsync(gridQuery, cancellationToken));
    }

    [HttpGet("done")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetDoneAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetDoneAsync(gridQuery, cancellationToken));
    }

    [HttpGet("mine")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetMineAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetMineAsync(gridQuery, cancellationToken));
    }

    [HttpGet("delegated")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetDelegatedAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetDelegatedAsync(gridQuery, cancellationToken));
    }

    [HttpGet("timeout")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetTimeoutAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetTimeoutAsync(gridQuery, cancellationToken));
    }

    [HttpGet("cc")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetCcAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetCcAsync(gridQuery, cancellationToken));
    }

    [HttpGet("process-instances/{processInstanceId}")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetByProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetByProcessInstanceAsync(processInstanceId, cancellationToken));
    }

    [HttpGet("{taskId}/detail")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetDetailAsync(string taskId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetDetailAsync(taskId, cancellationToken));
    }

    [HttpGet("attachments/{attachmentId}/download")]
    [Permission(PermissionCodes.WorkflowTaskAttachment)]
    public async Task<IActionResult> DownloadAttachmentAsync(string attachmentId, CancellationToken cancellationToken)
    {
        var result = await workflowTaskService.DownloadAttachmentAsync(attachmentId, cancellationToken);
        var fileName = string.IsNullOrWhiteSpace(result.Metadata.Name)
            ? $"{attachmentId}.bin"
            : result.Metadata.Name;
        Response.Headers["X-Trace-Id"] = HttpContext.TraceIdentifier;
        return File(result.Content, "application/octet-stream", fileName);
    }

    [HttpPost("{taskId}/claim")]
    [Permission(PermissionCodes.WorkflowTaskClaim)]
    public async Task<IActionResult> ClaimAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.ClaimAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/unclaim")]
    [Permission(PermissionCodes.WorkflowTaskClaim)]
    public async Task<IActionResult> UnclaimAsync(string taskId, CancellationToken cancellationToken)
    {
        await workflowTaskService.UnclaimAsync(taskId, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/complete")]
    [Permission(PermissionCodes.WorkflowTaskApprove)]
    public async Task<IActionResult> CompleteAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.CompleteAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/reject")]
    [Permission(PermissionCodes.WorkflowTaskApprove)]
    public async Task<IActionResult> RejectAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.RejectAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/return")]
    [Permission(PermissionCodes.WorkflowTaskApprove)]
    public async Task<IActionResult> ReturnAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.ReturnAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/transfer")]
    [Permission(PermissionCodes.WorkflowTaskTransfer)]
    public async Task<IActionResult> TransferAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.TransferAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/delegate")]
    [Permission(PermissionCodes.WorkflowTaskDelegate)]
    public async Task<IActionResult> DelegateAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.DelegateAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/resolve")]
    [Permission(PermissionCodes.WorkflowTaskDelegate)]
    public async Task<IActionResult> ResolveAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.ResolveAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/owner")]
    [Permission(PermissionCodes.WorkflowTaskDelegate)]
    public async Task<IActionResult> SetOwnerAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.SetOwnerAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{taskId}/add-sign")]
    [Permission(PermissionCodes.WorkflowTaskApprove)]
    public async Task<IActionResult> AddSignAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.AddSignAsync(taskId, request, cancellationToken));
    }

    [HttpGet("{taskId}/identity-links")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetIdentityLinksAsync(string taskId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetIdentityLinksAsync(taskId, cancellationToken));
    }

    [HttpPost("{taskId}/identity-links")]
    [Permission(PermissionCodes.WorkflowTaskApprove)]
    public async Task<IActionResult> AddIdentityLinkAsync(string taskId, [FromBody] WorkflowIdentityLinkRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.AddIdentityLinkAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpDelete("{taskId}/identity-links")]
    [Permission(PermissionCodes.WorkflowTaskApprove)]
    public async Task<IActionResult> DeleteIdentityLinkAsync(string taskId, [FromBody] WorkflowIdentityLinkRequest request, CancellationToken cancellationToken)
    {
        await workflowTaskService.DeleteIdentityLinkAsync(taskId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("{taskId}/comments")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetCommentsAsync(string taskId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetCommentsAsync(taskId, cancellationToken));
    }

    [HttpPost("{taskId}/comments")]
    [Permission(PermissionCodes.WorkflowTaskComment)]
    public async Task<IActionResult> AddCommentAsync(string taskId, [FromBody] WorkflowCommentRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.AddCommentAsync(taskId, request, cancellationToken));
    }

    [HttpGet("{taskId}/attachments")]
    [Permission(PermissionCodes.WorkflowTaskQuery)]
    public async Task<IActionResult> GetAttachmentsAsync(string taskId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.GetAttachmentsAsync(taskId, cancellationToken));
    }

    [HttpPost("{taskId}/attachments")]
    [Permission(PermissionCodes.WorkflowTaskAttachment)]
    public async Task<IActionResult> AddAttachmentAsync(string taskId, [FromBody] WorkflowAttachmentRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowTaskService.AddAttachmentAsync(taskId, request, cancellationToken));
    }
}
