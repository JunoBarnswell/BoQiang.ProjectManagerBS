using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_saved_views")]
public sealed class ProjectManagementSavedViewEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public string ViewKey { get; set; } = "tree";
    public string QueryJson { get; set; } = "{}";
    public string OwnerUserId { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public bool IsDefault { get; set; }
    public long VersionNo { get; set; } = 1;
}
