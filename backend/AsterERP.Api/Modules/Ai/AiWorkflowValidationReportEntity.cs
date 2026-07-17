using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_workflow_validation_reports")]
public sealed class AiWorkflowValidationReportEntity : EntityBase, IAiOwnedEntity
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

    public bool IsValid { get; set; }

    public int ErrorCount { get; set; }

    public int WarningCount { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string IssuesJson { get; set; } = "[]";
}
