using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_labels")]
public sealed class ProjectManagementLabelEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? ProjectId { get; set; }
    public string LabelName { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748B";
    public long VersionNo { get; set; } = 1;
}

[SugarTable("pm_task_labels")]
public sealed class ProjectManagementTaskLabelEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string LabelId { get; set; } = string.Empty;
    public long VersionNo { get; set; } = 1;
}
