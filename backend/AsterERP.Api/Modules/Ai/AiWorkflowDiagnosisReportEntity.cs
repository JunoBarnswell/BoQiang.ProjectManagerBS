using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_workflow_diagnosis_reports")]
public sealed class AiWorkflowDiagnosisReportEntity : EntityBase, IAiOwnedEntity
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

    public string TraceId { get; set; } = string.Empty;

    public string DiagnosisType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT")]
    public string EvidenceJson { get; set; } = "[]";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string SuggestionsJson { get; set; } = "[]";
}
