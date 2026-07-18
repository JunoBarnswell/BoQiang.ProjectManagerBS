namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskReminderCreateRequest(
    DateTimeOffset ReminderAt,
    string TimeZoneId,
    string RecipientScope,
    IReadOnlyList<string>? RecipientUserIds,
    string? Note,
    string ClientRequestId);

public sealed record ProjectManagementTaskReminderUpdateRequest(
    DateTimeOffset ReminderAt,
    string TimeZoneId,
    string? Note,
    long VersionNo);

public sealed record ProjectManagementTaskReminderResponse(
    string Id,
    string ProjectId,
    string TaskId,
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
