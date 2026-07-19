namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementReportQuery(
    int PageIndex = 1,
    int PageSize = 100,
    string? Keyword = null,
    string? Status = null,
    ProjectManagementTaskLabelFilter? LabelFilter = null);

public sealed record ProjectManagementReportRow(
    string ProjectCode,
    string ProjectName,
    string Status,
    string Priority,
    string OwnerUserId,
    decimal ProgressPercent,
    int TaskCount,
    DateTime? StartDate,
    DateTime? DueDate,
    DateTime CreatedTime,
    int EstimatedMinutes,
    int ActualMinutes);

public sealed record ProjectManagementReportFile(
    string FileName,
    string ContentType,
    byte[] Content,
    int RowCount);

public sealed record ProjectManagementReportSnapshotOptions(
    bool IncludeCompleted = false,
    bool IncludeDeleted = false,
    bool IncludeCommentSummary = false,
    bool IncludeAttachmentList = false,
    bool IncludeGanttSnapshot = false,
    int MaxTaskRows = 2000,
    int RetentionHours = 24);

public sealed record ProjectManagementReportSnapshotRequest(
    string Format,
    ProjectManagementReportQuery Query,
    ProjectManagementReportSnapshotOptions? Options = null);

public sealed record ProjectManagementReportSnapshotStartResponse(
    string OperationId,
    string TraceId,
    DateTime ExpiresAt);
