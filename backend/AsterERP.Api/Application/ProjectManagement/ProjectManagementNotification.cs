namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementNotification(
    string TenantId,
    string AppCode,
    string NotificationType,
    string RecipientUserId,
    string Title,
    string Message,
    string TargetRoute,
    string TraceId,
    string? ProjectId = null,
    string? TaskId = null);
