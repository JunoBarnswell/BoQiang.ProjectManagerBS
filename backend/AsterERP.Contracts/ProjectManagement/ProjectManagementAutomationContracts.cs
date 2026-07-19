using AsterERP.Contracts.Workflows;

namespace AsterERP.Contracts.ProjectManagement;

public static class ProjectManagementAutomationEntityTypes
{
    public const string Project = "Project";
    public const string Milestone = "Milestone";
    public const string Task = "Task";

    public static bool IsSupported(string value) => value is Project or Milestone or Task;
}

public static class ProjectManagementAutomationTriggerTypes
{
    public const string StatusChanged = "status-changed";
    public const string DueDate = "due-date";
    public const string AssigneeChanged = "assignee-changed";
    public const string MilestoneChanged = "milestone-changed";
}

public static class ProjectManagementAutomationActionTypes
{
    public const string StartApproval = "start-approval";
    public const string Webhook = "webhook";
}

public sealed record ProjectManagementAutomationRuleUpsertRequest(
    string? RuleId,
    bool Enabled,
    string EntityType,
    string Trigger,
    string? Status,
    string? AssigneeUserId,
    string? MilestoneId,
    int? DueWithinDays,
    string ActionType,
    string? WebhookUrl,
    string? WebhookSecret,
    Dictionary<string, string>? WebhookHeaders);

public sealed record ProjectManagementAutomationRuleResponse(
    string RuleId,
    bool Enabled,
    string EntityType,
    string Trigger,
    string? Status,
    string? AssigneeUserId,
    string? MilestoneId,
    int? DueWithinDays,
    string ActionType,
    string? WebhookUrl,
    bool WebhookSecretConfigured,
    Dictionary<string, string> WebhookHeaders);

public sealed record ProjectManagementAutomationRulesResponse(
    string EntityType,
    bool WorkflowBindingConfigured,
    string? ProcessDefinitionKey,
    IReadOnlyList<ProjectManagementAutomationRuleResponse> Rules);

public sealed record ProjectManagementAutomationRuleRunResponse(
    string RuleId,
    string EntityType,
    string EntityId,
    string Status,
    string TraceId,
    string? ErrorMessage,
    DateTime ExecutedAt);

public sealed record ProjectManagementAutomationExecutionLogResponse(
    string Id,
    string ActivityType,
    string? Summary,
    string TraceId,
    string ActorUserId,
    DateTime CreatedTime);

public sealed record ProjectManagementApprovalStartRequest(
    string? IdempotencyKey,
    string? Title,
    Dictionary<string, object?>? Variables);

public sealed record ProjectManagementApprovalResponse(
    string ProcessInstanceId,
    string BusinessType,
    string BusinessKey,
    string Status,
    string ProcessDefinitionKey,
    string? DetailRoute,
    bool Replayed);

public sealed record ProjectManagementAutomationDeliveryResponse(
    string DeliveryId,
    string Status,
    string EventType,
    string? ErrorMessage,
    DateTime StartedTime,
    DateTime? CompletedTime,
    int ProgressPercent);

public sealed record ProjectManagementAutomationReplayRequest(string? IdempotencyKey);

public sealed record ProjectManagementAutomationWebhookPayload(
    string EventType,
    string RuleId,
    string EntityType,
    string EntityId,
    string ProjectId,
    string? Status,
    string? AssigneeUserId,
    string? MilestoneId,
    long VersionNo,
    DateTime OccurredAt,
    string TraceId,
    Dictionary<string, object?> Data);
