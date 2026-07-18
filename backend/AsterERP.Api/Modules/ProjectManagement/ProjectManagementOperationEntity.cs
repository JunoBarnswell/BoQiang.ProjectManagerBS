using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_operations")]
public sealed class ProjectManagementOperationEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public string Phase { get; set; } = "Pending";
    public int ProgressPercent { get; set; }
    public long VersionNo { get; set; } = 1;
    public bool IsCancellationRequested { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? CancellationRequestedTime { get; set; }
    [SugarColumn(IsNullable = true)] public string? CancellationRequestedBy { get; set; }
    public string ImpactJson { get; set; } = "{}";
    [SugarColumn(IsNullable = true)] public string? ErrorMessage { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public DateTime StartedTime { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? CompletedTime { get; set; }
}
