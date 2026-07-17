using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_schedule_trigger_logs")]
public sealed class FlowiseScheduleTriggerLogEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string ScheduleRecordId { get; set; } = string.Empty;

    public string TriggerType { get; set; } = "AGENTFLOW";

    public string TargetId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ExecutionId { get; set; }

    public string Status { get; set; } = "QUEUED";

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? Error { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? ElapsedTimeMs { get; set; }

    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;
}
