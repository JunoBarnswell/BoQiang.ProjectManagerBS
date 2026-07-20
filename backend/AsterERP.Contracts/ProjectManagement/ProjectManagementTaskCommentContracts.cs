namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskCommentUpsertRequest(
    string Markdown,
    string? ParentCommentId = null,
    IReadOnlyList<string>? MentionUserIds = null,
    long VersionNo = 0);

public sealed record ProjectManagementTaskCommentQuery(
    int PageIndex = 1,
    int PageSize = 50,
    string Sort = "timeline");

public sealed record ProjectManagementTaskCommentResponse(
    string Id,
    string ProjectId,
    string TaskId,
    string? ParentCommentId,
    string Markdown,
    IReadOnlyList<ProjectManagementTaskCommentMentionResponse> Mentions,
    string AuthorUserId,
    long VersionNo,
    DateTime CreatedTime,
    DateTime? EditedTime,
    string? AuthorDisplayName = null);

public sealed record ProjectManagementTaskCommentMentionResponse(
    string UserId,
    string DisplayName);

public sealed record ProjectManagementTaskCommentMentionCandidateQuery(
    string? Keyword = null,
    int PageIndex = 1,
    int PageSize = 20);

public sealed record ProjectManagementTaskCommentMentionCandidateResponse(
    string UserId,
    string UserName,
    string DisplayName);
