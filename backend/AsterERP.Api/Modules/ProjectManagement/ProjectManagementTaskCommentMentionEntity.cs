using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_comment_mentions")]
public sealed class ProjectManagementTaskCommentMentionEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string CommentId { get; set; } = string.Empty;
    public string MentionedUserId { get; set; } = string.Empty;
    public string MentionedUserDisplayName { get; set; } = string.Empty;
}
