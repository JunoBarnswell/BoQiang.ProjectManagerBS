using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_tool_execution_logs")]
public sealed class AiToolExecutionLogEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ConversationId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RunId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ModelConfigId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PlanId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ItemId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AgentProfileId { get; set; }

    public string ToolName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ToolCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TraceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ArgumentsJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ResultSummary { get; set; }

    public bool RequiresConfirmation { get; set; } = true;

    [SugarColumn(IsNullable = true)]
    public string? ConfirmedBy { get; set; }

    public string Status { get; set; } = "Pending";

    public int DurationMs { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }
}
