using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_drafts")]
public sealed class ProjectManagementTaskDraftEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTime ExpiresAt { get; set; }
    public long VersionNo { get; set; } = 1;
}

[SugarTable("pm_task_draft_attachments")]
public sealed class ProjectManagementTaskDraftAttachmentEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string DraftId { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public long VersionNo { get; set; } = 1;
}
