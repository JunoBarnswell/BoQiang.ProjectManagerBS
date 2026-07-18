using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_operation_events")]
public sealed class ProjectManagementOperationEventEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public bool IsCancellationRequested { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
}
