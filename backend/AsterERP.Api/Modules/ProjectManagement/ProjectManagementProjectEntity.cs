using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_projects")]
public sealed class ProjectManagementProjectEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? Description { get; set; }
    public string Status { get; set; } = "Planning";
    public string Priority { get; set; } = "Medium";
    public string OwnerUserId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public DateTime? StartDate { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? DueDate { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? CompletedAt { get; set; }
    [SugarColumn(IsNullable = true)] public int? WipLimit { get; set; }
    public decimal ProgressPercent { get; set; }
    public long VersionNo { get; set; } = 1;
}
