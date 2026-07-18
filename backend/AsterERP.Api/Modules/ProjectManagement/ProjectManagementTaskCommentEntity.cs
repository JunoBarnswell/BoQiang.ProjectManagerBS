using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_comments")]
public sealed class ProjectManagementTaskCommentEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? ParentCommentId { get; set; }
    public string Markdown { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? MentionUserIdsJson { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public long VersionNo { get; set; } = 1;
    [SugarColumn(IsNullable = true)] public DateTime? EditedTime { get; set; }
}
