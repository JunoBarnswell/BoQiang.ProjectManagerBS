using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_purge_file_deletions")]
public sealed class ProjectManagementPurgeFileDeletionEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? CompletedTime { get; set; }
    [SugarColumn(IsNullable = true)] public string? LastError { get; set; }
}
