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
    IReadOnlyList<ProjectManagementOverviewPersonSummary> People);
