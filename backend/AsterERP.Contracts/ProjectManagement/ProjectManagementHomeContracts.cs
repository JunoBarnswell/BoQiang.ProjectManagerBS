namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementHomeQuery(
    string Collection = "all",
    string? Keyword = null,
    string? Health = null,
    string? Priority = null,
    string? LeadUserId = null,
    string? Status = null,
    DateTime? TargetDateFrom = null,
    DateTime? TargetDateTo = null,
    bool IncludeArchived = false,
    string SortBy = "updated",
    string SortDirection = "desc",
    int PageIndex = 1,
    int PageSize = 50);

public sealed record ProjectManagementHomeProjectItem(
    string Id,
    string ProjectCode,
    string ProjectName,
    string Status,
    string Priority,
    string Health,
    string OwnerUserId,
    string? OwnerDisplayName,
    DateTime? StartDate,
    DateTime? TargetDate,
    string? CurrentMilestoneId,
    string? CurrentMilestoneName,
    int IssueCount,
    int OpenIssueCount,
    int CompletedIssueCount,
    decimal ProgressPercent,
    DateTime? UpdatedTime,
    long VersionNo);

public sealed record ProjectManagementHomeProjectsResponse(
    IReadOnlyList<ProjectManagementHomeProjectItem> Items,
    int Total,
    int PageIndex,
    int PageSize,
    long Sequence);

public sealed record ProjectManagementHomeHealthSummary(string Key, int Count);

public sealed record ProjectManagementHomeLeadSummary(
    string? UserId,
    string DisplayName,
    int Count);

public sealed record ProjectManagementHomeSummaryResponse(
    IReadOnlyList<ProjectManagementHomeHealthSummary> Health,
    IReadOnlyList<ProjectManagementHomeLeadSummary> Leads,
    int UnassignedCount,
    long Sequence);
