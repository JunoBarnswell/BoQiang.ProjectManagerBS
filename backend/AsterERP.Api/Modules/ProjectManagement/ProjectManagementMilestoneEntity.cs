using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_milestones")]
public sealed class ProjectManagementMilestoneEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? OwnerUserId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? Description { get; set; }
    public string Status { get; set; } = "Planned";
    public string HealthStatus { get; set; } = "OnTrack";
    [SugarColumn(IsNullable = true)] public DateTime? StartDate { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? DueDate { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? CompletedAt { get; set; }
    public decimal ProgressPercent { get; set; }
    public int SortOrder { get; set; }
    public long VersionNo { get; set; } = 1;
}
