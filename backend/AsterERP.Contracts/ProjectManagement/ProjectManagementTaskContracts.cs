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
    bool IncludeCompleted = true,
    ProjectManagementTaskLabelFilter? LabelFilter = null);

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
    /// <summary>
    /// 显式进度权重。未提供时服务端使用正的 <see cref="EstimateMinutes"/>，否则回退为 1。
    /// </summary>
    decimal? Weight = null,
    int? EstimateMinutes = null,
    long VersionNo = 0,
    bool OverrideWip = false,
    bool ForceComplete = false,
    string? ForceCompleteReason = null);

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

/// <summary>删除任务时对子任务的处理策略。</summary>
public static class ProjectManagementTaskDeleteModes
{
    public const string Cascade = "Cascade";
    public const string PromoteChildren = "PromoteChildren";
}

/// <summary>
/// <see cref="ProjectManagementTaskDeleteModes.Cascade"/> 软删除完整子树；
/// <see cref="ProjectManagementTaskDeleteModes.PromoteChildren"/> 删除当前任务并提升直接子任务。
/// </summary>
public sealed record ProjectManagementTaskDeleteRequest(long VersionNo, string Mode = ProjectManagementTaskDeleteModes.Cascade);

/// <summary>
/// 任务工作台列表投影。刻意不包含描述、工时和审计字段；选中任务后应读取详情接口。
/// </summary>
public sealed record ProjectManagementTaskListItemResponse(
    string Id,
    string ProjectId,
    string? MilestoneId,
    string? ParentTaskId,
    string TaskCode,
    string Title,
    string Status,
    string Priority,
    string? AssigneeUserId,
    DateTime? StartDate,
    DateTime? DueDate,
    decimal ProgressPercent,
    int SortOrder,
    int Depth,
    long VersionNo,
    int BlockedByCount,
    bool CanStart,
    string? BlockedReason,
    bool IsOverdue = false,
    DateTime? ActualStartAt = null,
    DateTime? ActualEndAt = null);

/// <summary>
/// 单任务详情，用于编辑和任务侧栏；列表加载不返回这些扩展字段。
/// </summary>
public sealed record ProjectManagementTaskDetailResponse(
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
    int BlockedByCount,
    bool CanStart,
    string? BlockedReason,
    bool IsOverdue = false,
    DateTime? ActualStartAt = null,
    DateTime? ActualEndAt = null);

/// <summary>保留给既有批量、模板和我的任务契约的完整任务载荷。</summary>
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
    string? BlockedReason = null,
    bool IsOverdue = false);
