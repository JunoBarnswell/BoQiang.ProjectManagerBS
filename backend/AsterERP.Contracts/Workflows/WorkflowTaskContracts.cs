namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowTaskListItemResponse(
    string Id,
    string? Name,
    string? Assignee,
    string? Owner,
    string? DelegationState,
    string? ProcessInstanceId,
    string? ProcessDefinitionId,
    string? ExecutionId,
    string? TaskDefinitionKey,
    int Priority,
    DateTime? CreatedAt,
    DateTime? DueAt,
    IReadOnlyList<WorkflowIdentityLinkResponse> IdentityLinks)
{
    public string? BusinessType { get; init; }

    public string? BusinessKey { get; init; }

    public string? ProcessName { get; init; }

    public string? StarterUserName { get; init; }

    public string? AssigneeName { get; init; }

    public IReadOnlyList<string> CandidateNames { get; init; } = [];

    public IReadOnlyList<string> AvailableActions { get; init; } = [];

    public bool IsClaimable { get; init; }

    public bool IsOverdue { get; init; }

    public int CommentsCount { get; init; }

    public int AttachmentsCount { get; init; }
}

public sealed record WorkflowTaskSummaryResponse(
    int Todo,
    int Done,
    int Mine,
    int Delegated,
    int Timeout,
    int Cc,
    int History);

public sealed record WorkflowTaskDetailResponse(
    WorkflowTaskListItemResponse Task,
    WorkflowSubmittedFormResponse SubmittedForm,
    IReadOnlyList<WorkflowCommentResponse> Comments,
    IReadOnlyList<WorkflowAttachmentResponse> Attachments,
    IReadOnlyList<WorkflowTimelineItemResponse> Timeline,
    WorkflowTaskNodePolicyResponse NodePolicy);

public sealed record WorkflowTaskNodePolicyResponse(
    string? TaskDefinitionKey,
    IReadOnlyList<WorkflowTaskActionPolicyResponse> ActionPolicies,
    IReadOnlyList<WorkflowTaskFieldPermissionResponse> FieldPermissions)
{
    public static WorkflowTaskNodePolicyResponse Empty(string? taskDefinitionKey) => new(taskDefinitionKey, [], []);
}

public sealed record WorkflowTaskActionPolicyResponse(
    string Action,
    bool Enabled,
    bool CommentRequired,
    string AttachmentPolicy);

public sealed record WorkflowTaskFieldPermissionResponse(
    string FieldKey,
    string? FieldLabel,
    bool Visible,
    bool Readonly,
    bool Required,
    bool Hidden);

public sealed record WorkflowSubmittedFormResponse(
    string Source,
    IReadOnlyList<WorkflowSubmittedFormFieldResponse> Fields);

public sealed record WorkflowSubmittedFormFieldResponse(
    string Field,
    string Label,
    object? Value,
    string? ValueType);

public sealed record WorkflowTaskActionRequest(
    string? UserId,
    string? TargetUserId,
    string? Comment,
    Dictionary<string, object?>? Variables);

public sealed record WorkflowIdentityLinkRequest(
    string? UserId,
    string? GroupId,
    string Type);

public sealed record WorkflowIdentityLinkResponse(
    string Id,
    string? UserId,
    string? GroupId,
    string? Type,
    string? TaskId,
    string? ProcessInstanceId,
    string? ProcessDefinitionId);

public sealed record WorkflowCommentRequest(string Message, string? Type);

public sealed record WorkflowCommentResponse(
    string Id,
    string? TaskId,
    string? ProcessInstanceId,
    string? Type,
    string? UserId,
    string? Message,
    DateTime? Time);

public sealed record WorkflowAttachmentRequest(
    string? AttachmentType,
    string? Name,
    string? Description,
    string? Url,
    string? Base64Content);

public sealed record WorkflowAttachmentResponse(
    string Id,
    string? TaskId,
    string? ProcessInstanceId,
    string? Name,
    string? Description,
    string? Type,
    string? Url)
{
    public bool HasContent { get; init; }

    public string? DownloadUrl { get; init; }

    public DateTime? CreatedAt { get; init; }
}
