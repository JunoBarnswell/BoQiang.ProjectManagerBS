using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("wf_message_template")]
public sealed class WorkflowMessageTemplateEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public string ChannelType { get; set; } = "in-app";

    [SugarColumn(IsNullable = true)]
    public string? SubjectTemplate { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string BodyTemplate { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? VariablesJson { get; set; }

    public bool IsEnabled { get; set; } = true;
}
