using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.Agent;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai")]
public sealed class AiTaskPlansController(
    IAiTaskPlanService service,
    AiPlanGenerationService generationService,
    IAiAgentExecutionService executionService) : BaseApiController
{
    [HttpGet("conversations/{conversationId}/task-plans")]
    [Permission(PermissionCodes.AiChatView)]
    public async Task<IActionResult> GetByConversationAsync(string conversationId, CancellationToken cancellationToken) =>
        ApiOk(await service.GetByConversationAsync(conversationId, cancellationToken));

    [HttpGet("task-plans/{planId}")]
    [Permission(PermissionCodes.AiChatView)]
    public async Task<IActionResult> GetDetailAsync(string planId, [FromQuery] bool includeEvents, CancellationToken cancellationToken) =>
        ApiOk(await service.GetDetailAsync(planId, includeEvents, cancellationToken));

    [HttpGet("task-plans/{planId}/events")]
    [Permission(PermissionCodes.AiTaskPlanLogView)]
    public async Task<IActionResult> GetEventsAsync(string planId, [FromQuery] long? afterSeq, [FromQuery] int pageSize, CancellationToken cancellationToken) =>
        ApiOk(await service.GetEventsAsync(planId, afterSeq, pageSize, cancellationToken));

    [HttpGet("task-plans/{planId}/outputs")]
    [Permission(PermissionCodes.AiTaskPlanLogView)]
    public async Task<IActionResult> GetOutputsAsync(string planId, [FromQuery] string? itemId, [FromQuery] int pageIndex, [FromQuery] int pageSize, CancellationToken cancellationToken) =>
        ApiOk(await service.GetOutputsAsync(planId, itemId, pageIndex, pageSize, cancellationToken));

    [HttpPost("conversations/{conversationId}/task-plans")]
    [Permission(PermissionCodes.AiTaskPlanCreate)]
    public async Task<IActionResult> CreateAsync(string conversationId, [FromBody] AiTaskPlanUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.CreateAsync(conversationId, request, null, cancellationToken));

    [HttpPost("conversations/{conversationId}/task-plans/generate")]
    [Permission(PermissionCodes.AiTaskPlanCreate)]
    public async Task<IActionResult> GenerateAsync(string conversationId, [FromBody] AiTaskPlanGenerateRequest request, CancellationToken cancellationToken) =>
        ApiOk(await generationService.GenerateAsync(conversationId, request, cancellationToken));

    [HttpPut("task-plans/{planId}")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> UpdateAsync(string planId, [FromBody] AiTaskPlanUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateAsync(planId, request, cancellationToken));

    [HttpPost("task-plans/{planId}/replan")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> ReplanAsync(string planId, [FromBody] AiTaskPlanUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateAsync(planId, request, cancellationToken));

    [HttpPost("task-plans/{planId}/duplicate")]
    [Permission(PermissionCodes.AiTaskPlanCreate)]
    public async Task<IActionResult> DuplicateAsync(string planId, CancellationToken cancellationToken) =>
        ApiOk(await service.DuplicateAsync(planId, cancellationToken));

    [HttpDelete("task-plans/{planId}")]
    [Permission(PermissionCodes.AiTaskPlanDelete)]
    public async Task<IActionResult> DeleteAsync(string planId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(planId, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("task-plans/{planId}/items")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> AddItemAsync(string planId, [FromBody] AiTaskPlanItemUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.AddItemAsync(planId, request, cancellationToken));

    [HttpPatch("task-plan-items/{itemId}")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> PatchItemAsync(string itemId, [FromBody] AiTaskPlanItemPatchRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.PatchItemAsync(itemId, request, cancellationToken));

    [HttpPost("task-plan-items/{itemId}/move")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> MoveItemAsync(string itemId, [FromBody] AiTaskPlanMoveRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.MoveItemAsync(itemId, request, cancellationToken));

    [HttpPost("task-plan-items/{itemId}/split")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> SplitItemAsync(string itemId, [FromBody] AiTaskPlanSplitRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.SplitItemAsync(itemId, request, cancellationToken));

    [HttpPost("task-plan-items/{itemId}/merge")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> MergeItemsAsync(string itemId, [FromBody] AiTaskPlanMergeRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.MergeItemsAsync(itemId, request, cancellationToken));

    [HttpDelete("task-plan-items/{itemId}")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> DeleteItemAsync(string itemId, [FromQuery] int? expectedRevision, CancellationToken cancellationToken)
    {
        await service.DeleteItemAsync(itemId, expectedRevision, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("task-plans/{planId}/approve")]
    [Permission(PermissionCodes.AiTaskPlanApprove)]
    public async Task<IActionResult> ApproveAsync(string planId, CancellationToken cancellationToken) =>
        ApiOk(await service.ApproveAsync(planId, cancellationToken));

    [HttpPost("task-plans/{planId}/unapprove")]
    [Permission(PermissionCodes.AiTaskPlanApprove)]
    public async Task<IActionResult> UnapproveAsync(string planId, CancellationToken cancellationToken) =>
        ApiOk(await service.UnapproveAsync(planId, cancellationToken));

    [HttpPost("task-plans/{planId}/execute")]
    [Permission(PermissionCodes.AiTaskPlanExecute)]
    public async Task<IActionResult> ExecuteAsync(string planId, [FromBody] AiTaskPlanItemActionRequest? request, CancellationToken cancellationToken) =>
        ApiOk(await executionService.ExecuteAsync(planId, Guid.NewGuid().ToString("N"), userInstruction: request?.ExecutionHint, cancellationToken: cancellationToken));

    [HttpPost("task-plans/{planId}/pause")]
    [Permission(PermissionCodes.AiTaskPlanExecute)]
    public async Task<IActionResult> PauseAsync(string planId, CancellationToken cancellationToken) =>
        ApiOk(await service.PauseAsync(planId, cancellationToken));

    [HttpPost("task-plans/{planId}/resume")]
    [Permission(PermissionCodes.AiTaskPlanExecute)]
    public async Task<IActionResult> ResumeAsync(string planId, CancellationToken cancellationToken) =>
        ApiOk(await service.ResumeAsync(planId, cancellationToken));

    [HttpPost("task-plans/{planId}/cancel")]
    [Permission(PermissionCodes.AiTaskPlanExecute)]
    public async Task<IActionResult> CancelAsync(string planId, CancellationToken cancellationToken) =>
        ApiOk(await service.CancelAsync(planId, cancellationToken));

    [HttpPost("task-plan-items/{itemId}/mark-complete")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> MarkCompleteAsync(string itemId, [FromBody] AiTaskPlanItemActionRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.MarkCompleteAsync(itemId, request, cancellationToken));

    [HttpPost("task-plan-items/{itemId}/retry")]
    [Permission(PermissionCodes.AiTaskPlanRetry)]
    public async Task<IActionResult> RetryAsync(string itemId, [FromBody] AiTaskPlanItemActionRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.RetryAsync(itemId, request, cancellationToken));

    [HttpPost("task-plan-items/{itemId}/skip")]
    [Permission(PermissionCodes.AiTaskPlanSkip)]
    public async Task<IActionResult> SkipAsync(string itemId, [FromBody] AiTaskPlanItemActionRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.SkipAsync(itemId, request, cancellationToken));

    [HttpPost("task-plan-items/{itemId}/block")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> BlockAsync(string itemId, [FromBody] AiTaskPlanItemActionRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.BlockAsync(itemId, request, cancellationToken));

    [HttpPost("task-plan-items/{itemId}/unblock")]
    [Permission(PermissionCodes.AiTaskPlanEdit)]
    public async Task<IActionResult> UnblockAsync(string itemId, [FromBody] AiTaskPlanItemActionRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.UnblockAsync(itemId, request, cancellationToken));
}
