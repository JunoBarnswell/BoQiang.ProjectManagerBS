using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_followers")]
public sealed class ProjectManagementTaskFollowerEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public long VersionNo { get; set; } = 1;
}
