using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("wf_node_notification_rule")]
public sealed class WorkflowNodeNotificationRuleEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ModelId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ProcessDefinitionKey { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string Trigger { get; set; } = "node-enter";

    public string ReceiverType { get; set; } = "starter";

    [SugarColumn(IsNullable = true)]
    public string? ReceiverValue { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ChannelCodesJson { get; set; } = "[]";

    public string TemplateCode { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? ConditionJson { get; set; }

    public string FailurePolicy { get; set; } = "ignore";

    public bool IsEnabled { get; set; } = true;
}
