using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

internal sealed record ProjectManagementReportPdfDocument(
    DateTime GeneratedAt,
    string TenantId,
    string AppCode,
    string UserId,
    IReadOnlyList<ProjectManagementReportRow> Projects,
    IReadOnlyList<ProjectManagementReportPdfMilestone> Milestones,
    IReadOnlyList<ProjectManagementReportPdfTask> Tasks,
    IReadOnlyDictionary<string, int> TaskStatusDistribution,
    int FutureDueCount,
    int OverdueCount,
    int BlockedCount,
    IReadOnlyList<ProjectManagementReportPdfTask> CriticalPath,
    IReadOnlyList<string> CommentSummaries,
    IReadOnlyList<string> AttachmentList,
    bool IncludeGanttSnapshot,
    bool IncludeDeleted);

internal sealed record ProjectManagementReportPdfMilestone(
    string ProjectId,
    string Name,
    string Status,
    decimal ProgressPercent,
    DateTime? DueDate);

internal sealed record ProjectManagementReportPdfTask(
    string Id,
    string ProjectId,
    string TaskCode,
    string Title,
    string Status,
    string Priority,
    string? AssigneeUserId,
    DateTime? StartDate,
    DateTime? DueDate,
    decimal ProgressPercent,
    int Depth,
    bool IsBlocked,
    bool IsDeleted);
