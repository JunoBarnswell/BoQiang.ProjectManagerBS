using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("workflow_callback_logs")]
public sealed class WorkflowCallbackLogEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProcessInstanceId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkflowTaskId { get; set; }

    public string ProcessDefinitionKey { get; set; } = string.Empty;

    public string Trigger { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? NodeId { get; set; }

    public string RuleId { get; set; } = string.Empty;

    public string TargetModelCode { get; set; } = string.Empty;

    public string TargetKey { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
