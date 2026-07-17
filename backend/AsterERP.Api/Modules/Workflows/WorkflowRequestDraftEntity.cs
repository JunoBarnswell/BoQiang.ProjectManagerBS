using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("workflow_request_drafts")]
public sealed class WorkflowRequestDraftEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string FormResourceCode { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? BusinessKey { get; set; }

    public string Title { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT")]
    public string DraftJson { get; set; } = "{}";

    public string Status { get; set; } = "Draft";

    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? SubmittedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ProcessInstanceId { get; set; }
}
