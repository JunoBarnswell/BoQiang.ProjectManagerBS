using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("workflow_model_extensions")]
public sealed class WorkflowModelExtensionEntity : EntityBase
{
    public string ModelId { get; set; } = string.Empty;

    public string ModelKey { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AppCode { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? ExtensionJson { get; set; }
}
