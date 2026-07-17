namespace AsterERP.Contracts.Ai;

public sealed class AiTaskPlanDto
{
    public string Id { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string? RunId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public string Mode { get; set; } = "Plan";

    public int VersionNo { get; set; } = 1;

    public int Revision { get; set; }

    public string ExecutionStrategy { get; set; } = "Serial";

    public string? RisksJson { get; set; }

    public string? AssumptionsJson { get; set; }

    public string? MetadataJson { get; set; }

    public string? ApprovedBy { get; set; }

    public int? ApprovedRevision { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }

    public AiTaskPlanProgressDto Progress { get; set; } = new();

    public IReadOnlyList<AiTaskPlanItemDto> Items { get; set; } = [];

    public IReadOnlyList<AiTaskPlanEventDto> Events { get; set; } = [];
}

public sealed class AiTaskPlanItemDto
{
    public string Id { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public string? ParentItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string Priority { get; set; } = "P1";

    public string OwnerType { get; set; } = "Agent";

    public string TaskType { get; set; } = "Design";

    public int SortOrder { get; set; }

    public int Depth { get; set; }

    public string? DependsOnJson { get; set; }

    public string? AcceptanceCriteriaJson { get; set; }

    public string? ToolCode { get; set; }

    public string? ExecutionHint { get; set; }

    public string? Result { get; set; }

    public string? ResultSummary { get; set; }

    public string? EvidenceJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? BlockedReason { get; set; }

    public string? SkipReason { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetryCount { get; set; } = 3;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class AiTaskPlanUpsertRequest
{
    public string Title { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public string Mode { get; set; } = "Plan";

    public string ExecutionStrategy { get; set; } = "Serial";

    public string? RisksJson { get; set; }

    public string? AssumptionsJson { get; set; }

    public string? MetadataJson { get; set; }

    public int? ExpectedRevision { get; set; }

    public IReadOnlyList<AiTaskPlanItemUpsertRequest> Items { get; set; } = [];
}

public sealed class AiTaskPlanItemUpsertRequest
{
    public string? Id { get; set; }

    public string? ParentItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string Priority { get; set; } = "P1";

    public string OwnerType { get; set; } = "Agent";

    public string TaskType { get; set; } = "Design";

    public int SortOrder { get; set; }

    public string? DependsOnJson { get; set; }

    public string? AcceptanceCriteriaJson { get; set; }

    public string? ToolCode { get; set; }

    public string? ExecutionHint { get; set; }

    public int MaxRetryCount { get; set; } = 3;
}

public sealed class AiTaskPlanItemPatchRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public string? Priority { get; set; }

    public string? OwnerType { get; set; }

    public string? TaskType { get; set; }

    public int? SortOrder { get; set; }

    public string? ParentItemId { get; set; }

    public string? DependsOnJson { get; set; }

    public string? AcceptanceCriteriaJson { get; set; }

    public string? ToolCode { get; set; }

    public string? ExecutionHint { get; set; }

    public string? Result { get; set; }

    public string? ResultSummary { get; set; }

    public string? EvidenceJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? BlockedReason { get; set; }

    public string? SkipReason { get; set; }

    public string? UserResult { get; set; }

    public int? ExpectedRevision { get; set; }

    public DateTime? ExpectedUpdatedTime { get; set; }
}

public sealed class AiTaskPlanProgressDto
{
    public int TotalCount { get; set; }

    public int CompletedCount { get; set; }

    public int FailedCount { get; set; }

    public int BlockedCount { get; set; }

    public int WaitingUserCount { get; set; }

    public int Percent { get; set; }
}

public sealed class AiTaskPlanEventDto
{
    public string Id { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public string? ItemId { get; set; }

    public string? RunId { get; set; }

    public long Seq { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public string? Summary { get; set; }

    public string? PayloadJson { get; set; }

    public string? TraceId { get; set; }

    public string? OperatorUserId { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class AiTaskPlanItemOutputDto
{
    public string Id { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    public string? RunId { get; set; }

    public string OutputType { get; set; } = "Text";

    public string ResultSummary { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string? EvidenceJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class AiTaskPlanItemActionRequest
{
    public string? Reason { get; set; }

    public string? UserResult { get; set; }

    public string? ExecutionHint { get; set; }

    public DateTime? ExpectedUpdatedTime { get; set; }
}

public sealed class AiTaskPlanMoveRequest
{
    public string? ParentItemId { get; set; }

    public int SortOrder { get; set; }

    public int? ExpectedRevision { get; set; }
}

public sealed class AiTaskPlanSplitRequest
{
    public int? ExpectedRevision { get; set; }

    public IReadOnlyList<AiTaskPlanItemUpsertRequest> Items { get; set; } = [];
}

public sealed class AiTaskPlanMergeRequest
{
    public int? ExpectedRevision { get; set; }

    public IReadOnlyList<string> SourceItemIds { get; set; } = [];

    public string? Title { get; set; }

    public string? Description { get; set; }
}

public sealed class AiTaskPlanGenerateRequest
{
    public string Content { get; set; } = string.Empty;

    public string? ModelConfigId { get; set; }

    public string? PromptTemplateId { get; set; }

    public string? ClientMessageId { get; set; }

    public string? IdempotencyKey { get; set; }
}
