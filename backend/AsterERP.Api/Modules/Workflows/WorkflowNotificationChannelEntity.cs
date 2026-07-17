using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("wf_notification_channel")]
public sealed class WorkflowNotificationChannelEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ChannelCode { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;

    public string ChannelType { get; set; } = "in-app";

    public bool IsEnabled { get; set; } = true;

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? ConfigJson { get; set; }

    public string FailurePolicy { get; set; } = "ignore";
}
