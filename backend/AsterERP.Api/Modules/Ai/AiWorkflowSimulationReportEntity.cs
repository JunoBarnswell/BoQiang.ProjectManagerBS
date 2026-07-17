using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_workflow_simulation_reports")]
public sealed class AiWorkflowSimulationReportEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? RunId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PlanId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PlanItemId { get; set; }

    public string DraftArtifactId { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string VariablesJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string StepsJson { get; set; } = "[]";
}
