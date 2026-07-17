using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_task_plan_item_outputs")]
public sealed class AiTaskPlanItemOutputEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public string ItemId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? RunId { get; set; }

    public string OutputType { get; set; } = "Text";

    public string ResultSummary { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Content { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? EvidenceJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }
}
