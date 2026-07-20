using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_project_subscriptions")]
public sealed class ProjectManagementProjectSubscriptionEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Mode { get; set; } = "AllUpdates";
    public long VersionNo { get; set; } = 1;
}
