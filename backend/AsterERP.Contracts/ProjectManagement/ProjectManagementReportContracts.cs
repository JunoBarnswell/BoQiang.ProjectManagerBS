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

/// <summary>
/// 单项目 Markdown 汇报导出选项。该格式面向人工阅读和交给 AI 进行总结，
/// 不包含附件二进制内容或审计敏感字段。
/// </summary>
public sealed record ProjectManagementProjectMarkdownOptions(
    bool IncludeProjectInfo = true,
    bool IncludeCompleted = true,
    bool IncludeComments = true,
    bool IncludeActivities = true,
    int MaxTaskRows = 500,
    int MaxCommentRows = 1000,
    int MaxActivityRows = 1000,
    string? TaskIds = null);

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
