using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_sync_journal")]
public sealed class ProjectManagementSyncJournalEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public long SequenceNo { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? ProjectId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public long VersionNo { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? DeviceId { get; set; }
    public string TraceId { get; set; } = string.Empty;
}
