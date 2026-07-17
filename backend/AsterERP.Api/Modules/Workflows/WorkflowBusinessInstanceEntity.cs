using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("workflow_business_instances")]
public sealed class WorkflowBusinessInstanceEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public string BusinessKey { get; set; } = string.Empty;

    public string ProcessInstanceId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    public string ProcessDefinitionKey { get; set; } = string.Empty;

    public string Status { get; set; } = "Running";

    public string StartedBy { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedAt { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? VariableSnapshotJson { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? SubmittedFormJson { get; set; }
}
