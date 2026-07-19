namespace AsterERP.Contracts.ProjectManagement;

public static class ProjectManagementWebhookEventTypes
{
    public const string ProjectChanged = "project.changed";
    public const string MilestoneChanged = "milestone.changed";
    public const string TaskChanged = "task.changed";
    public const string StatusChanged = "status.changed";
    public const string CommentCreated = "comment.created";
    public const string AttachmentCreated = "attachment.created";
    public const string ReminderSent = "reminder.sent";
    public const string SyncCompleted = "sync.completed";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    { ProjectChanged, MilestoneChanged, TaskChanged, StatusChanged, CommentCreated, AttachmentCreated, ReminderSent, SyncCompleted };
}

public sealed record ProjectManagementWebhookSubscriptionUpsertRequest(string? Id, string ProjectId, string Name, string EndpointUrl, string? Secret, IReadOnlyList<string> EventTypes, bool IsEnabled, int MaxAttempts = 5);
public sealed record ProjectManagementWebhookSubscriptionResponse(string Id, string ProjectId, string Name, string EndpointUrl, bool SecretConfigured, IReadOnlyList<string> EventTypes, bool IsEnabled, int MaxAttempts, DateTime CreatedTime, DateTime? UpdatedTime);
public sealed record ProjectManagementWebhookDeliveryResponse(string EventId, string SubscriptionId, string ProjectId, string EventType, string Status, int AttemptCount, int MaxAttempts, DateTime NextAttemptAt, string? ErrorMessage, DateTime CreatedTime, DateTime? CompletedTime);
public sealed record ProjectManagementWebhookReplayRequest(string? Reason);
public sealed record ProjectManagementWebhookEventPayload(string EventId, string EventType, DateTimeOffset OccurredAt, string ProjectId, string ResourceType, string ResourceId, string TraceId, IReadOnlyDictionary<string, string?> Data);
