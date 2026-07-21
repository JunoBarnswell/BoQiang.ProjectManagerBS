namespace AsterERP.Contracts.ProjectManagement;

public static class ProjectManagementExcelImportTemplate
{
    public const string Version = "1.0";
    public const string ReadmeSheet = "README";
    public const string ProjectsSheet = "Projects";
    public const string TasksSheet = "Tasks";
    public const string MembersSheet = "Members";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Columns { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [ProjectsSheet] = ["StableId", "ProjectCode", "ProjectName", "Description", "Status", "Priority", "OwnerUserId", "StartDate", "DueDate", "WipLimit", "ProgressPercent", "VersionNo"],
            [TasksSheet] = ["StableId", "ProjectId", "MilestoneId", "ParentTaskId", "TaskCode", "Title", "Summary", "Description", "Status", "Priority", "AssigneeUserId", "AssigneeEmploymentId", "StartDate", "DueDate", "ProgressPercent", "Weight", "SortOrder", "VersionNo", "Labels", "DependencyIds"],
            [MembersSheet] = ["StableId", "ProjectId", "UserId", "EmploymentId", "RoleCode", "ScopeRootTaskId", "IsActive", "JoinedAt", "LeftAt", "VersionNo"]
        };
}

public static class ProjectManagementExcelImportPreviewStatuses
{
    public const string Completed = "Completed";
    public const string CompletedWithErrors = "CompletedWithErrors";
}

public sealed record ProjectManagementExcelTemplateFile(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record ProjectManagementExcelImportRowError(
    string SheetName,
    int RowNumber,
    string? StableId,
    string Code,
    string Message,
    string Severity);

public sealed record ProjectManagementExcelImportPreviewResponse(
    string PreviewId,
    string Status,
    string TemplateVersion,
    DateTime ParsedAt,
    int TotalRows,
    int ImportableRows,
    int DuplicateRows,
    int ErrorRows,
    int WarningRows,
    int NewRows,
    int UpdatedRows,
    int SkippedRows,
    IReadOnlyList<ProjectManagementExcelImportRowError> Errors,
    bool ErrorsTruncated);

public sealed record ProjectManagementExcelImportSnapshotRow(
    string SheetName,
    int RowNumber,
    string? StableId,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<ProjectManagementExcelImportRowError> Issues);

public sealed record ProjectManagementExcelImportSnapshot(
    ProjectManagementExcelImportPreviewResponse Preview,
    IReadOnlyList<ProjectManagementExcelImportSnapshotRow> Rows);

public sealed record ProjectManagementExcelImportConfirmRequest(
    string PreviewId,
    string IdempotencyKey);

public static class ProjectManagementExcelImportResultStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Replayed = "Replayed";
}

public static class ProjectManagementExcelImportRowStatuses
{
    public const string Added = "Added";
    public const string Updated = "Updated";
    public const string Skipped = "Skipped";
    public const string Failed = "Failed";
    public const string Conflict = "Conflict";
    public const string Warning = "Warning";
}

public sealed record ProjectManagementExcelImportRowResult(
    string SheetName,
    int RowNumber,
    string? StableId,
    string Status,
    string? Message,
    long? VersionNo);

public sealed record ProjectManagementExcelImportResultResponse(
    string ImportId,
    string PreviewId,
    string IdempotencyKey,
    string Status,
    string TraceId,
    DateTime CompletedAt,
    int AddedRows,
    int UpdatedRows,
    int SkippedRows,
    int FailedRows,
    int ConflictRows,
    int WarningRows,
    IReadOnlyList<ProjectManagementExcelImportRowResult> Rows,
    bool Replayed);
