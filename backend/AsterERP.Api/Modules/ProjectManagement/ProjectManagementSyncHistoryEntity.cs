using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_sync_history")]
public sealed class ProjectManagementSyncHistoryEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string SourceTenantId { get; set; } = string.Empty;
    public string SourceAppCode { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? SourceDeviceId { get; set; }
    public string TargetTenantId { get; set; } = string.Empty;
    public string TargetAppCode { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public int Skipped { get; set; }
    public int ConflictCount { get; set; }
    public int Failed { get; set; }
    public int AttachmentsImported { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public string ReportJson { get; set; } = "{}";
    [SugarColumn(IsNullable = true)] public string? ErrorMessage { get; set; }
    [SugarColumn(IsNullable = true)] public string? RetryOfHistoryId { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
