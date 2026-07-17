using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("wf_notification_task")]
public sealed class WorkflowNotificationTaskEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? RuleId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? WorkflowTaskId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? NodeId { get; set; }

    public string Trigger { get; set; } = string.Empty;

    public string ChannelCode { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ReceiverUserId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ReceiverAddress { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Subject { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string Content { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int RetryCount { get; set; }

    public int MaxRetryCount { get; set; } = 3;

    public DateTime DueAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? SentAt { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? LastError { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? VariablesJson { get; set; }
}
