using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

/// <summary>
/// Grants a non-project member read access to exactly one task. It is created
/// by an explicit external @mention and never grants task-management rights.
/// </summary>
[SugarTable("pm_task_grants")]
public sealed class ProjectManagementTaskGrantEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string GranteeUserId { get; set; } = string.Empty;
    public bool CanComment { get; set; }
    public string GrantedByUserId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? SourceCommentId { get; set; }
    public bool IsActive { get; set; } = true;
    public long VersionNo { get; set; } = 1;
}
