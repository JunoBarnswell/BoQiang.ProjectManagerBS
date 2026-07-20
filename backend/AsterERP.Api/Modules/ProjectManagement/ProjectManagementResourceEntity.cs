using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_project_resources")]
public sealed class ProjectManagementResourceEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceUrl { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? Description { get; set; }
    public long VersionNo { get; set; } = 1;
}
