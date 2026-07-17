using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_task_plan_events")]
public sealed class AiTaskPlanEventEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ItemId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RunId { get; set; }

    public long Seq { get; set; }

    public string EventName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? FromStatus { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ToStatus { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Summary { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PayloadJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TraceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? OperatorUserId { get; set; }
}
