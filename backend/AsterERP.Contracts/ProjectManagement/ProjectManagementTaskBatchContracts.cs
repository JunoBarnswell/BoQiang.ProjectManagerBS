namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskBatchItem(string TaskId, long VersionNo);

public sealed record ProjectManagementTaskBatchUpdateRequest(
    string ProjectId,
    IReadOnlyList<ProjectManagementTaskBatchItem> Items,
    string? Status = null,
    string? Priority = null,
    string? AssigneeUserId = null,
    bool OverrideWip = false,
    string? OverrideWipReason = null,
    string? MilestoneId = null,
    bool UpdateMilestone = false,
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    bool UpdateSchedule = false,
    IReadOnlyList<string>? LabelIds = null,
    bool UpdateLabels = false,
    string? ParentTaskId = null,
    bool UpdateParent = false,
    string? BeforeTaskId = null,
    IReadOnlyList<string>? ParticipantUserIds = null,
    bool ReplaceParticipants = false,
    bool ForceComplete = false,
    string? ForceCompleteReason = null,
    string Operation = ProjectManagementTaskBatchOperations.Update,
    string DeleteMode = ProjectManagementTaskDeleteModes.Cascade);

public static class ProjectManagementTaskBatchOperations
{
    public const string Update = "update";
    public const string Delete = "delete";
}

public static class ProjectManagementTaskBatchResultStatuses
{
    public const string Succeeded = "succeeded";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
    public const string Conflict = "conflict";
}

public sealed record ProjectManagementTaskBatchItemResult(
    string TaskId,
    string? TaskCode,
    string Status,
    string? Message,
    int? ErrorCode,
    long? VersionNo);

public sealed record ProjectManagementTaskBatchExecutionResult(
    string OperationId,
    string ProjectId,
    int RequestedCount,
    int SucceededCount,
    int SkippedCount,
    int FailedCount,
    int ConflictCount,
    IReadOnlyList<ProjectManagementTaskBatchItemResult> Items);
