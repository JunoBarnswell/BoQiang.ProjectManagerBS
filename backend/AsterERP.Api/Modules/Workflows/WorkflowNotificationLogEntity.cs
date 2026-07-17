using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("wf_notification_log")]
public sealed class WorkflowNotificationLogEntity : EntityBase
{
    [SugarColumn(IsNullable = true)]
    public string? NotificationTaskId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RuleId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? WorkflowTaskId { get; set; }

    public string ChannelCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ReceiverUserId { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string Result { get; set; } = "Pending";

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? Message { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Provider { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TraceId { get; set; }
}
