namespace AsterERP.Contracts.ProjectManagement;

/// <summary>甘特图的原子排程命令。每项携带自己的目标日期与乐观并发版本，禁止使用公共批量字段覆盖不同任务的日期。</summary>
public sealed record ProjectManagementGanttScheduleBatchUpdateRequest(
    string ProjectId,
    IReadOnlyList<ProjectManagementGanttScheduleTaskChange> Items);

public sealed record ProjectManagementGanttScheduleTaskChange(
    string TaskId,
    DateTime StartDate,
    DateTime DueDate,
    long VersionNo);

public sealed record ProjectManagementGanttScheduleBatchUpdateResponse(
    string ProjectId,
    IReadOnlyList<ProjectManagementGanttScheduleTaskResult> Items);

public sealed record ProjectManagementGanttScheduleTaskResult(
    string TaskId,
    DateTime StartDate,
    DateTime DueDate,
    long VersionNo);
