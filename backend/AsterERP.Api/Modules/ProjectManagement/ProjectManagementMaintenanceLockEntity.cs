using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_maintenance_locks")]
public sealed class ProjectManagementMaintenanceLockEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string LockKey { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
