using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("workflow_bindings")]
public sealed class WorkflowBindingEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public string ProcessDefinitionKey { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ModelId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ModelKey { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? FormResourceCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PageCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ModelCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? KeyField { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DetailRoute { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TitleTemplate { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? StatusField { get; set; }

    public bool IsEnabled { get; set; } = true;

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? StartFormJson { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? BindingConfigJson { get; set; }
}
