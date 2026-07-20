using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_project_members")]
public sealed class ProjectManagementProjectMemberEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? EmploymentId { get; set; }
    public string RoleCode { get; set; } = "Member";
    [SugarColumn(IsNullable = true)] public string? ScopeRootTaskId { get; set; }
    /// <summary>项目内建议周容量，单位为分钟；用于真实工作负载投影。</summary>
    public int SuggestedCapacityMinutes { get; set; } = 2400;
    public bool IsActive { get; set; } = true;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    [SugarColumn(IsNullable = true)] public DateTime? LeftAt { get; set; }
    public long VersionNo { get; set; } = 1;
}
