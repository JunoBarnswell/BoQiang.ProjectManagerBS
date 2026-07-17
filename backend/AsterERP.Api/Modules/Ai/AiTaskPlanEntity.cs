using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_task_plans")]
public sealed class AiTaskPlanEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? RunId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public string Mode { get; set; } = "Plan";

    public int VersionNo { get; set; } = 1;

    public int Revision { get; set; }

    public string ExecutionStrategy { get; set; } = "Serial";

    [SugarColumn(IsNullable = true)]
    public string? RisksJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AssumptionsJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? MetadataJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ApprovedBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? ApprovedRevision { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ApprovedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedAt { get; set; }
}
