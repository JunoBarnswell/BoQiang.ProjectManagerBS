using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_participants")]
public sealed class ProjectManagementTaskParticipantEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? EmploymentId { get; set; }
    public string RoleCode { get; set; } = "Participant";
    public long VersionNo { get; set; } = 1;
}
