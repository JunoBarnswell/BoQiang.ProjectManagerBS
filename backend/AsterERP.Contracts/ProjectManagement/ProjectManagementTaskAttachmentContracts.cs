namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskAttachmentResponse(
    string Id,
    string ProjectId,
    string TaskId,
    string FileId,
    string FileName,
    string ContentType,
    long FileSize,
    string DownloadUrl,
    string PreviewUrl,
    string UploadedByUserId,
    DateTime CreatedTime,
    long VersionNo,
    bool PreviewSupported = false,
    string? PreviewType = null,
    string? PreviewPipeline = null,
    string? CommentId = null);
