using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_task_plan_items")]
public sealed class AiTaskPlanItemEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ParentItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string Priority { get; set; } = "P1";

    public string OwnerType { get; set; } = "Agent";

    public string TaskType { get; set; } = "Design";

    public int SortOrder { get; set; }

    public int Depth { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DependsOnJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AcceptanceCriteriaJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ToolCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ExecutionHint { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Result { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ResultSummary { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? EvidenceJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? BlockedReason { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SkipReason { get; set; }

    public int RetryCount { get; set; }

    public int MaxRetryCount { get; set; } = 3;

    [SugarColumn(IsNullable = true)]
    public DateTime? StartedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedAt { get; set; }
}
