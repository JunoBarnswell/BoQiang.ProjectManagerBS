using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_schedule_records")]
public sealed class FlowiseScheduleRecordEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string TriggerType { get; set; } = "AGENTFLOW";

    public string TargetId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? NodeId { get; set; }

    public string CronExpression { get; set; } = string.Empty;

    public string Timezone { get; set; } = "UTC";

    public bool Enabled { get; set; } = true;

    public string ScheduleInputMode { get; set; } = "text";

    [SugarColumn(IsNullable = true)]
    public string? DefaultInput { get; set; }

    [SugarColumn(IsNullable = true, ColumnDataType = "TEXT")]
    public string? DefaultForm { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastRunAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? NextRunAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? EndDate { get; set; }
}
