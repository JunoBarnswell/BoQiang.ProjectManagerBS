namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskQuery(
    string ProjectId,
    int PageIndex = 1,
    int PageSize = 50,
    string? Keyword = null,
    string? Status = null,
    string? AssigneeUserId = null,
    string ViewKey = "tree",
    string? GroupBy = null,
    string SortBy = "tree",
    string SortDirection = "asc",
    string? MilestoneId = null,
    string? ParentTaskId = null,
    DateTime? DueFrom = null,
    DateTime? DueTo = null,
    bool IncludeCompleted = true);

public sealed record ProjectManagementTaskUpsertRequest(
    string TaskCode,
    string Title,
    string? Description = null,
    string Status = "Todo",
    string Priority = "Medium",
    string? MilestoneId = null,
    string? ParentTaskId = null,
    string? AssigneeUserId = null,
    string? AssigneeEmploymentId = null,
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    decimal ProgressPercent = 0,
    decimal Weight = 1,
    int? EstimateMinutes = null,
    long VersionNo = 0,
    bool OverrideWip = false);

/// <summary>
/// 移动任务树。<paramref name="BeforeTaskId"/> 是稳定排序的首选定位方式；
/// <paramref name="SortOrder"/> 仅为旧客户端提供回退插入序号。
/// </summary>
public sealed record ProjectManagementTaskMoveRequest(
    string? ParentTaskId,
    int SortOrder,
    long VersionNo,
    string? BeforeTaskId = null,
    string? MilestoneId = null,
    bool UpdateMilestone = false);

public sealed record ProjectManagementTaskResponse(
    string Id,
    string ProjectId,
    string? MilestoneId,
    string? ParentTaskId,
    string TaskCode,
    string Title,
    string? Description,
    string Status,
    string Priority,
    string? AssigneeUserId,
    string? AssigneeEmploymentId,
    DateTime? StartDate,
    DateTime? DueDate,
    decimal ProgressPercent,
    decimal Weight,
    int? EstimateMinutes,
    int ActualMinutes,
    int SortOrder,
    int Depth,
    long VersionNo,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    int BlockedByCount = 0,
    bool CanStart = true,
    string? BlockedReason = null);
