namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskBatchItem(string TaskId, long VersionNo);

public sealed record ProjectManagementTaskBatchUpdateRequest(
    string ProjectId,
    IReadOnlyList<ProjectManagementTaskBatchItem> Items,
    string? Status = null,
    string? Priority = null,
    string? AssigneeUserId = null,
    bool OverrideWip = false,
    string? MilestoneId = null,
    bool UpdateMilestone = false,
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    bool UpdateSchedule = false,
    IReadOnlyList<string>? LabelIds = null,
    bool UpdateLabels = false);
