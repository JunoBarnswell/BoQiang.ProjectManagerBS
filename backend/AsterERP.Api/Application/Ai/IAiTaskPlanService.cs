using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai;

public interface IAiTaskPlanService
{
    Task<IReadOnlyList<AiTaskPlanDto>> GetByConversationAsync(string conversationId, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> GetDetailAsync(string planId, bool includeEvents = false, CancellationToken cancellationToken = default);

    Task<GridPageResult<AiTaskPlanEventDto>> GetEventsAsync(string planId, long? afterSeq, int pageSize, CancellationToken cancellationToken = default);

    Task<GridPageResult<AiTaskPlanItemOutputDto>> GetOutputsAsync(string planId, string? itemId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> CreateAsync(string conversationId, AiTaskPlanUpsertRequest request, string? runId = null, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> CreateFromAssistantContentAsync(AiConversationEntity conversation, string runId, string content, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> UpdateAsync(string planId, AiTaskPlanUpsertRequest request, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> DuplicateAsync(string planId, CancellationToken cancellationToken = default);

    Task DeleteAsync(string planId, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> AddItemAsync(string planId, AiTaskPlanItemUpsertRequest request, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> PatchItemAsync(string itemId, AiTaskPlanItemPatchRequest request, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> MoveItemAsync(string itemId, AiTaskPlanMoveRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiTaskPlanItemDto>> SplitItemAsync(string itemId, AiTaskPlanSplitRequest request, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> MergeItemsAsync(string targetItemId, AiTaskPlanMergeRequest request, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(string itemId, int? expectedRevision, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> ApproveAsync(string planId, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> UnapproveAsync(string planId, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> PauseAsync(string planId, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> ResumeAsync(string planId, CancellationToken cancellationToken = default);

    Task<AiTaskPlanDto> CancelAsync(string planId, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> MarkCompleteAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> RetryAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> SkipAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> BlockAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default);

    Task<AiTaskPlanItemDto> UnblockAsync(string itemId, AiTaskPlanItemActionRequest request, CancellationToken cancellationToken = default);
}
