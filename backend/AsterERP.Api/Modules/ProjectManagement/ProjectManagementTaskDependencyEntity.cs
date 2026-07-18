using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_dependencies")]
public sealed class ProjectManagementTaskDependencyEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string PredecessorTaskId { get; set; } = string.Empty;
    public string SuccessorTaskId { get; set; } = string.Empty;
    public string DependencyType { get; set; } = "FinishToStart";
    public int LagMinutes { get; set; }
    public long VersionNo { get; set; } = 1;
}
