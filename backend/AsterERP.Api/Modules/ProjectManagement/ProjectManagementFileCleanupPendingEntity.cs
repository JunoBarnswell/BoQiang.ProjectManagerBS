using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

/// <summary>
/// 已提交业务删除后，等待删除外部文件存储对象的可靠待办。
/// </summary>
[SugarTable("pm_file_cleanup_pending")]
public sealed class ProjectManagementFileCleanupPendingEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? LastAttemptTime { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? CompletedTime { get; set; }
    [SugarColumn(IsNullable = true)] public string? LastError { get; set; }
}
