namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskFollowerResponse(string Id, string TaskId, string UserId, long VersionNo, DateTime CreatedTime);
public sealed record ProjectManagementTaskFollowerUpsertRequest(string UserId, long VersionNo = 0);
public sealed record ProjectManagementTaskDraftCreateRequest(string ProjectId, string PayloadJson = "{}", int ExpiresInHours = 24);
public sealed record ProjectManagementTaskDraftResponse(string Id, string ProjectId, string PayloadJson, DateTime ExpiresAt, long VersionNo);
public sealed record ProjectManagementTaskDraftAttachmentResponse(string Id, string DraftId, string FileName, string ContentType, long FileSize, long VersionNo, DateTime CreatedTime);
