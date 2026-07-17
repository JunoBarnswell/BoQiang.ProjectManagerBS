using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_workflow_draft_artifacts")]
public sealed class AiWorkflowDraftArtifactEntity : EntityBase, IAiOwnedEntity
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

    public string WorkflowKey { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string DraftDslJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? BpmnXml { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? BusinessCanvasJson { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? BindingProposalJson { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? FormPermissionProposalJson { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? ActionMappingProposalJson { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? NotificationPreviewJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ImportedWorkflowModelId { get; set; }
}
