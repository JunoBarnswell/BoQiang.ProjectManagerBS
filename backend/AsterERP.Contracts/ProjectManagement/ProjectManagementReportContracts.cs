namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementReportQuery(
    int PageIndex = 1,
    int PageSize = 100,
    string? Keyword = null,
    string? Status = null);

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
    DateTime CreatedTime);

public sealed record ProjectManagementReportFile(
    string FileName,
    string ContentType,
    byte[] Content,
    int RowCount);
