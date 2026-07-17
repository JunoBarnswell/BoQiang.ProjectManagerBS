using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("workflow_categories")]
public sealed class WorkflowCategoryEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string CategoryCode { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ParentCode { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;
}
