namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementImConversationEnsureRequest(string? TaskId);

public sealed record ProjectManagementImConversationResponse(
    string Id,
    string ProjectId,
    string? TaskId,
    string ConversationId,
    string ConversationType,
    string Title,
    string Status,
    string TargetRoute,
    long VersionNo);

public sealed record ProjectManagementImConversationTargetResponse(
    bool IsAvailable,
    string? ProjectId,
    string? TaskId,
    string? TargetRoute);
