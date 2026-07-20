namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementProjectReminderCreateRequest(
    DateTimeOffset ReminderAt,
    string TimeZoneId,
    string? Note,
    string ClientRequestId);

public sealed record ProjectManagementProjectReminderResponse(
    string Id,
    string ProjectId,
    string RecipientUserId,
    DateTime ReminderAtUtc,
    string TimeZoneId,
    string? Note,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTime? LastAttemptAt,
    DateTime? TriggeredAt,
    string? LastError,
    long VersionNo,
    DateTime CreatedTime);
