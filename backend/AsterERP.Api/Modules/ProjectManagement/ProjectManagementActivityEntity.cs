using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_activities")]
public sealed class ProjectManagementActivityEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? Summary { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
}
