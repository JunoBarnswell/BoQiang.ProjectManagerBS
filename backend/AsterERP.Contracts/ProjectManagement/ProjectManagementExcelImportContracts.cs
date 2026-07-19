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
            [TasksSheet] = ["StableId", "ProjectId", "MilestoneId", "ParentTaskId", "TaskCode", "Title", "Summary", "Description", "Status", "Priority", "AssigneeUserId", "AssigneeEmploymentId", "StartDate", "DueDate", "ProgressPercent", "Weight", "EstimateMinutes", "ActualMinutes", "SortOrder", "VersionNo", "Labels", "DependencyIds"],
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
