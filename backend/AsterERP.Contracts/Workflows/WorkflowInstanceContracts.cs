namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowStartInstanceRequest(
    string TenantId,
    string AppCode,
    string MenuCode,
    string BusinessType,
    string BusinessKey,
    string? Title,
    Dictionary<string, object?>? Variables);

public sealed record WorkflowInstanceResponse(
    string Id,
    string TenantId,
    string AppCode,
    string MenuCode,
    string BusinessType,
    string BusinessKey,
    string ProcessInstanceId,
    string? ProcessDefinitionId,
    string ProcessDefinitionKey,
    string Status,
    string StartedBy,
    DateTime StartedAt,
    DateTime? FinishedAt,
    Dictionary<string, object?> Variables,
    IReadOnlyList<WorkflowTaskListItemResponse> RuntimeTasks,
    IReadOnlyList<WorkflowActivityResponse> Activities)
{
    public string? StartedByName { get; init; }

    public IReadOnlyList<WorkflowTimelineItemResponse> Timeline { get; init; } = [];

    public IReadOnlyList<WorkflowCommentResponse> Comments { get; init; } = [];

    public IReadOnlyList<WorkflowAttachmentResponse> Attachments { get; init; } = [];

    public IReadOnlyList<WorkflowIdentityLinkResponse> IdentityLinks { get; init; } = [];

    public IReadOnlyList<WorkflowNotificationTaskResponse> Notifications { get; init; } = [];

    public WorkflowSubmittedFormResponse SubmittedForm { get; init; } = new("empty", []);
}

public sealed record WorkflowTimelineItemResponse(
    string Id,
    string Kind,
    string Title,
    string? UserId,
    string? UserName,
    string? ActivityId,
    string? TaskId,
    string? Action,
    string? Comment,
    DateTime? CreatedAt,
    DateTime? FinishedAt,
    long? DurationInMillis,
    Dictionary<string, object?> Metadata);

public sealed record WorkflowInstanceListItemResponse(
    string Id,
    string TenantId,
    string AppCode,
    string MenuCode,
    string BusinessType,
    string BusinessKey,
    string ProcessInstanceId,
    string? ProcessDefinitionId,
    string ProcessDefinitionKey,
    string Status,
    string StartedBy,
    DateTime StartedAt,
    DateTime? FinishedAt);

public sealed record WorkflowInstanceVariableRequest(
    Dictionary<string, object?> Variables);

public sealed record WorkflowHighlightedDiagramResponse(
    string ProcessInstanceId,
    string BpmnXml,
    IReadOnlyList<string> ActiveActivityIds,
    IReadOnlyList<string> CompletedActivityIds);
