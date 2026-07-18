using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_sync_devices")]
public sealed class ProjectManagementSyncDeviceEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public long LastExportedSequenceNo { get; set; }
    public long LastAcknowledgedSequenceNo { get; set; }
    public DateTime LastSeenAt { get; set; }
}
