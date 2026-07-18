using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_backups")]
public sealed class ProjectManagementBackupEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string BackupName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Status { get; set; } = "Ready";
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}
