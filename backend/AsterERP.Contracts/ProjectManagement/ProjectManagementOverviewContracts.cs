namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementOverviewQuery(
    int PageIndex = 1,
    int PageSize = 20,
    string? ProjectId = null,
    string? Keyword = null);

public sealed record ProjectManagementOverviewMilestoneSummary(
    string Id,
    string Name,
    string Status,
    string HealthStatus,
    decimal ProgressPercent,
    DateTime? DueDate);

public sealed record ProjectManagementOverviewPersonSummary(
    string UserId,
    int TaskCount,
    int CompletedTaskCount,
    int OverdueTaskCount);

/// <summary>
/// 项目列表与详情共用的轻量风险摘要；仅基于有效叶子任务计算，避免父子任务重复计数。
/// </summary>
public sealed record ProjectManagementProjectRiskSummary(
    int OverdueTaskCount,
    int BlockedTaskCount,
    int DueSoonIncompleteTaskCount,
    int InProgressTaskCount,
    int? WipLimit,
    bool IsWipExceeded,
    int WipExceededBy,
    bool HasScheduleRisk);

public sealed record ProjectManagementOverviewItem(
    ProjectManagementProjectResponse Project,
    int TaskCount,
    int CompletedTaskCount,
    int InProgressTaskCount,
    int OverdueTaskCount,
    int BlockedTaskCount,
    decimal TaskProgressPercent,
    int MilestoneCount,
    int MemberCount,
    IReadOnlyList<ProjectManagementOverviewMilestoneSummary> Milestones,
    IReadOnlyList<ProjectManagementOverviewPersonSummary> People,
    ProjectManagementProjectRiskSummary RiskSummary);
