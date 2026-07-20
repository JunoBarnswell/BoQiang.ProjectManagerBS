namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementNotificationQuery(
    int PageIndex = 1,
    int PageSize = 20,
    bool UnreadOnly = false,
    string? NotificationType = null);

public sealed record ProjectManagementNotificationResponse(
    string Id,
    string NotificationType,
    string Title,
    string Message,
    string TargetRoute,
    string TraceId,
    string? ProjectId,
    string? TaskId,
    bool IsRead,
    DateTime CreatedTime,
    DateTime? ReadTime,
    ProjectManagementLocalizedText? TitleText = null,
    ProjectManagementLocalizedText? MessageText = null);

public sealed record ProjectManagementNotificationPageResponse(
    int Total,
    int UnreadCount,
    IReadOnlyList<ProjectManagementNotificationResponse> Items);

public sealed record ProjectManagementNotificationOpenResponse(
    bool IsAvailable,
    string? TargetRoute,
    string? UnavailableReason,
    ProjectManagementLocalizedText? UnavailableReasonText = null);
