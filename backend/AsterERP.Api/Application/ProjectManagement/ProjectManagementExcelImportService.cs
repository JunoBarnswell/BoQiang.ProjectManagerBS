using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementExcelImportService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementMemberCandidateService? memberCandidateService = null) : IProjectManagementExcelImportService, ITransientDependency
{
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private const int MaxSheetCount = 4;
    private const int MaxRowsPerSheet = 5000;
    private const int MaxTotalRows = 5000;
    private const int MaxCellLength = 4000;
    private const int MaxErrorDetails = 5000;
    private static readonly TimeSpan MaxParseTime = TimeSpan.FromSeconds(30);

    public Task<ProjectManagementExcelTemplateFile> DownloadTemplateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook();
        var readme = workbook.Worksheets.Add(ProjectManagementExcelImportTemplate.ReadmeSheet);
        readme.Cell(1, 1).Value = "ProjectManagement Excel Import Template";
        readme.Cell(2, 1).Value = "TemplateVersion";
        readme.Cell(2, 2).Value = ProjectManagementExcelImportTemplate.Version;
        readme.Cell(4, 1).Value = "Projects / Tasks / Members 使用 StableId 作为稳定 ID；日期使用 ISO 8601；Labels 和 DependencyIds 使用分号分隔。";
        readme.Cell(5, 1).Value = "本模板仅用于上传解析和预览，预览阶段不会写入项目管理业务数据。";
        readme.Cell(6, 1).Value = "公式、重复表头、非法类型、超长内容、层级/依赖循环和无权数据会标记为错误。";
        readme.Column(1).Width = 110;
        readme.Row(1).Style.Font.Bold = true;
        foreach (var sheet in ProjectManagementExcelImportTemplate.Columns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worksheet = workbook.Worksheets.Add(sheet.Key);
            for (var index = 0; index < sheet.Value.Count; index++)
            {
                worksheet.Cell(1, index + 1).Value = sheet.Value[index];
                worksheet.Cell(1, index + 1).Style.Font.Bold = true;
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents(1, 36);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(new ProjectManagementExcelTemplateFile(
            "project-management-import-template-v1.0.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray()));
    }

    public async Task<ProjectManagementExcelImportPreviewResponse> PreviewAsync(IFormFile file, CancellationToken cancellationToken = default)
        => (await CreateSnapshotAsync(file, cancellationToken)).Preview;

    public async Task<ProjectManagementExcelImportSnapshot> CreateSnapshotAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ValidateFile(file);
        await using var uploadStream = file.OpenReadStream();
        using var workbookStream = new MemoryStream();
        await uploadStream.CopyToAsync(workbookStream, cancellationToken);
        workbookStream.Position = 0;
        cancellationToken.ThrowIfCancellationRequested();
        return await ParseWorkbookAsync(workbookStream, cancellationToken);
    }

    private async Task<ProjectManagementExcelImportSnapshot> ParseWorkbookAsync(Stream stream, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ValidationException($"Excel 文件无法解析：{exception.Message}");
        }

        using (workbook)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (workbook.Worksheets.Count > MaxSheetCount)
                throw new ValidationException($"Excel Sheet 数量不能超过 {MaxSheetCount}");

            var globalIssues = new List<RowIssue>();
            var rows = new List<ImportRow>();
            foreach (var sheet in ProjectManagementExcelImportTemplate.Columns)
            {
                var worksheet = workbook.Worksheets.FirstOrDefault(item => string.Equals(item.Name, sheet.Key, StringComparison.OrdinalIgnoreCase));
                if (worksheet is null)
                {
                    globalIssues.Add(new RowIssue(sheet.Key, 1, null, "MissingSheet", $"缺少 {sheet.Key} Sheet", "Error"));
                    continue;
                }

                rows.AddRange(ReadRows(worksheet, sheet.Key, sheet.Value, globalIssues, stopwatch, cancellationToken));
                if (rows.Count > MaxTotalRows)
                    throw new ValidationException($"Excel 数据行数不能超过 {MaxTotalRows}");
            }

            ValidateRows(rows, globalIssues, stopwatch, cancellationToken);
            var context = await LoadExistingDataAsync(rows, cancellationToken);
            await ValidateReferencesAsync(rows, context, globalIssues, stopwatch, cancellationToken);

            var errors = globalIssues.Concat(rows.SelectMany(item => item.Issues)).ToList();
            var errorRows = rows.Count(item => item.HasError);
            var warningRows = rows.Count(item => item.Issues.Any(issue => issue.Severity == "Warning"));
            var hasGlobalError = globalIssues.Any(item => item.Severity == "Error");
            var importableRows = hasGlobalError ? 0 : rows.Count(item => !item.HasError);
            var newRows = hasGlobalError ? 0 : CountRows(rows, context, existing: false);
            var updatedRows = hasGlobalError ? 0 : CountRows(rows, context, existing: true);
            var skippedRows = hasGlobalError ? rows.Count : errorRows;
            var limitedErrors = errors
                .Take(MaxErrorDetails)
                .Select(item => new ProjectManagementExcelImportRowError(item.SheetName, item.RowNumber, item.StableId, item.Code, item.Message, item.Severity))
                .ToList();

            var preview = new ProjectManagementExcelImportPreviewResponse(
                CreateSnapshotId(rows, errors),
                errors.Count == 0 ? ProjectManagementExcelImportPreviewStatuses.Completed : ProjectManagementExcelImportPreviewStatuses.CompletedWithErrors,
                ProjectManagementExcelImportTemplate.Version,
                DateTime.UtcNow,
                rows.Count,
                importableRows,
                rows.Count(item => item.Issues.Any(issue => issue.Code == "DuplicateStableId")),
                errorRows + globalIssues.Count,
                warningRows,
                newRows,
                updatedRows,
                skippedRows,
                limitedErrors,
                errors.Count > limitedErrors.Count);
            var snapshotRows = rows.Select(row => new ProjectManagementExcelImportSnapshotRow(
                row.SheetName,
                row.RowNumber,
                row.StableId,
                new Dictionary<string, string>(row.Values, StringComparer.OrdinalIgnoreCase),
                row.Issues.Select(issue => new ProjectManagementExcelImportRowError(issue.SheetName, issue.RowNumber, issue.StableId, issue.Code, issue.Message, issue.Severity)).ToList())).ToList();
            return new ProjectManagementExcelImportSnapshot(preview, snapshotRows);
        }
    }

    private static string CreateSnapshotId(IReadOnlyList<ImportRow> rows, IReadOnlyList<RowIssue> issues)
    {
        var builder = new StringBuilder();
        foreach (var row in rows.OrderBy(item => item.SheetName, StringComparer.Ordinal).ThenBy(item => item.RowNumber))
        {
            builder.Append(row.SheetName).Append('\u001f').Append(row.RowNumber).Append('\u001f').Append(row.StableId).Append('\u001f');
            foreach (var value in row.Values.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                builder.Append(value.Key).Append('=').Append(value.Value).Append('\u001e');
            foreach (var issue in row.Issues.OrderBy(item => item.Code, StringComparer.Ordinal))
                builder.Append(issue.Code).Append('=').Append(issue.Message).Append('\u001e');
            builder.Append('\n');
        }
        foreach (var issue in issues.OrderBy(item => item.SheetName, StringComparer.Ordinal).ThenBy(item => item.RowNumber).ThenBy(item => item.Code, StringComparer.Ordinal))
            builder.Append(issue.SheetName).Append('\u001f').Append(issue.RowNumber).Append('\u001f').Append(issue.Code).Append('=').Append(issue.Message).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private async Task<ExistingData> LoadExistingDataAsync(IReadOnlyList<ImportRow> rows, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetCurrentDb();
        var projectIds = Ids(rows, ProjectManagementExcelImportTemplate.ProjectsSheet);
        var taskIds = Ids(rows, ProjectManagementExcelImportTemplate.TasksSheet);
        var memberIds = Ids(rows, ProjectManagementExcelImportTemplate.MembersSheet);
        var projectRows = projectIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementProjectEntity>().Where(item => projectIds.Contains(item.Id) && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted).ToListAsync(cancellationToken);
        var taskRows = taskIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementTaskEntity>().Where(item => taskIds.Contains(item.Id) && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted).ToListAsync(cancellationToken);
        var memberRows = memberIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => memberIds.Contains(item.Id) && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted).ToListAsync(cancellationToken);
        var milestoneIds = rows.Select(item => item.Value("MilestoneId")).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct(StringComparer.Ordinal).ToList();
        var milestoneRows = milestoneIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => milestoneIds.Contains(item.Id) && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted).ToListAsync(cancellationToken);
        var labelIds = rows.SelectMany(item => Tokens(item.Value("Labels"))).Distinct(StringComparer.Ordinal).ToList();
        var labelRows = labelIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementLabelEntity>().Where(item => labelIds.Contains(item.Id) && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted).ToListAsync(cancellationToken);
        var knownProjectIds = projectRows.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        knownProjectIds.UnionWith(rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.ProjectsSheet).Select(item => item.StableId).Where(item => item is not null).Cast<string>());
        var dependencyRows = knownProjectIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementTaskDependencyEntity>().Where(item => knownProjectIds.Contains(item.ProjectId) && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted).ToListAsync(cancellationToken);
        return new ExistingData(projectRows, taskRows, memberRows, milestoneRows, labelRows, dependencyRows);
    }

    private async Task ValidateReferencesAsync(IReadOnlyList<ImportRow> rows, ExistingData existing, List<RowIssue> globalIssues, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var projectRows = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.ProjectsSheet).ToList();
        var taskRows = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.TasksSheet).ToList();
        var memberRows = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.MembersSheet).ToList();
        var projectIds = existing.Projects.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        projectIds.UnionWith(projectRows.Select(item => item.StableId).Where(item => item is not null).Cast<string>());
        var taskProjects = existing.Tasks.ToDictionary(item => item.Id, item => item.ProjectId, StringComparer.Ordinal);
        foreach (var row in taskRows)
        {
            CheckBudget(stopwatch, cancellationToken);
            var projectId = row.Value("ProjectId");
            if (string.IsNullOrWhiteSpace(projectId) || !projectIds.Contains(projectId)) AddError(row, "ProjectNotFound", "任务引用的项目不存在或不在当前授权范围");
            var parentId = row.Value("ParentTaskId");
            if (!string.IsNullOrWhiteSpace(parentId))
            {
                var parent = taskRows.FirstOrDefault(item => string.Equals(item.StableId, parentId, StringComparison.Ordinal))?.Value("ProjectId");
                if (parent is null && !taskProjects.TryGetValue(parentId, out parent)) AddError(row, "ParentTaskNotFound", "父任务不存在或不在当前授权范围");
                else if (!string.Equals(parent, projectId, StringComparison.Ordinal)) AddError(row, "ParentProjectMismatch", "父任务必须属于同一项目");
            }

            var milestoneId = row.Value("MilestoneId");
            if (!string.IsNullOrWhiteSpace(milestoneId) && !existing.Milestones.Any(item => item.Id == milestoneId && item.ProjectId == projectId))
                AddError(row, "MilestoneNotFound", "里程碑不存在或不属于引用项目");
            ValidateLabels(row, projectId, existing.Labels);
            foreach (var dependencyId in Tokens(row.Value("DependencyIds")))
            {
                if (dependencyId == row.StableId || (!taskRows.Any(item => item.StableId == dependencyId) && !taskProjects.ContainsKey(dependencyId)))
                    AddError(row, "DependencyNotFound", $"依赖任务不存在：{dependencyId}");
            }
        }

        var memberCandidateCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var row in memberRows)
        {
            CheckBudget(stopwatch, cancellationToken);
            var projectId = row.Value("ProjectId");
            if (string.IsNullOrWhiteSpace(projectId) || !projectIds.Contains(projectId)) AddError(row, "ProjectNotFound", "成员引用的项目不存在或不在当前授权范围");
            var scopeRoot = row.Value("ScopeRootTaskId");
            if (!string.IsNullOrWhiteSpace(scopeRoot) && !taskProjects.ContainsKey(scopeRoot) && !taskRows.Any(item => item.StableId == scopeRoot)) AddError(row, "ScopeRootTaskNotFound", "成员范围根任务不存在");
            var userId = row.Value("UserId");
            if (!string.IsNullOrWhiteSpace(userId))
            {
                if (!memberCandidateCache.TryGetValue(userId, out var selectable))
                {
                    if (memberCandidateService is null) throw new InvalidOperationException("成员候选服务未注册");
                    selectable = await memberCandidateService.IsSelectableAsync(userId, cancellationToken);
                    memberCandidateCache[userId] = selectable;
                }

                if (!selectable) AddError(row, "UserNotSelectable", "成员用户不是当前租户/应用下的启用用户");
            }
        }

        var hierarchyGraph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in taskRows.Where(item => !string.IsNullOrWhiteSpace(item.StableId)))
        {
            var parentId = row.Value("ParentTaskId");
            if (!string.IsNullOrWhiteSpace(parentId)) AddEdge(hierarchyGraph, row.StableId!, parentId);
        }
        AddCycleErrors(taskRows, hierarchyGraph, "HierarchyCycle", "任务父子层级存在循环");

        var dependencyGraph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var dependency in existing.Dependencies) AddEdge(dependencyGraph, dependency.PredecessorTaskId, dependency.SuccessorTaskId);
        foreach (var row in taskRows.Where(item => !string.IsNullOrWhiteSpace(item.StableId)))
            foreach (var dependencyId in Tokens(row.Value("DependencyIds"))) AddEdge(dependencyGraph, dependencyId, row.StableId!);
        AddCycleErrors(taskRows, dependencyGraph, "DependencyCycle", "任务依赖存在循环");
    }

    private static void ValidateLabels(ImportRow row, string? projectId, IReadOnlyList<ProjectManagementLabelEntity> labels)
    {
        foreach (var labelId in Tokens(row.Value("Labels")))
        {
            var label = labels.FirstOrDefault(item => item.Id == labelId);
            if (label is null || (label.ProjectId is not null && !string.Equals(label.ProjectId, projectId, StringComparison.Ordinal)))
                AddError(row, "LabelNotFound", $"标签不存在或不属于项目：{labelId}");
        }
    }

    private static void AddCycleErrors(IReadOnlyList<ImportRow> rows, Dictionary<string, List<string>> graph, string code, string message)
    {
        foreach (var node in FindCycleNodes(graph))
        {
            var row = rows.FirstOrDefault(item => item.StableId == node);
            if (row is not null) AddError(row, code, message);
        }
    }

    private static HashSet<string> FindCycleNodes(Dictionary<string, List<string>> graph)
    {
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        var cycles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Keys) Visit(node);
        return cycles;

        void Visit(string node)
        {
            if (states.TryGetValue(node, out var state))
            {
                if (state == 1)
                {
                    var index = stack.LastIndexOf(node);
                    if (index >= 0) cycles.UnionWith(stack.Skip(index));
                }
                return;
            }

            states[node] = 1;
            stack.Add(node);
            if (graph.TryGetValue(node, out var next)) foreach (var child in next) Visit(child);
            stack.RemoveAt(stack.Count - 1);
            states[node] = 2;
        }
    }

    private static void ValidateRows(IReadOnlyList<ImportRow> rows, List<RowIssue> globalIssues, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        foreach (var group in rows.Where(item => !string.IsNullOrWhiteSpace(item.StableId)).GroupBy(item => $"{item.SheetName}:{item.StableId}", StringComparer.Ordinal))
        {
            CheckBudget(stopwatch, cancellationToken);
            if (group.Count() > 1) foreach (var row in group) AddError(row, "DuplicateStableId", "同一 Sheet 内 StableId 重复");
        }

        foreach (var row in rows)
        {
            CheckBudget(stopwatch, cancellationToken);
            switch (row.SheetName)
            {
                case ProjectManagementExcelImportTemplate.ProjectsSheet:
                    Required(row, "StableId"); Required(row, "ProjectCode"); Required(row, "ProjectName");
                    Allowed(row, "Status", [ProjectManagementDomainRules.ProjectPlanning, ProjectManagementDomainRules.ProjectActive, ProjectManagementDomainRules.ProjectPaused, ProjectManagementDomainRules.ProjectCompleted, ProjectManagementDomainRules.ProjectCanceled, ProjectManagementDomainRules.ProjectArchived]);
                    Allowed(row, "Priority", ["Low", "Medium", "High", "Urgent"]);
                    Date(row, "StartDate"); Date(row, "DueDate"); NonNegativeInt(row, "WipLimit"); Percent(row, "ProgressPercent"); PositiveLong(row, "VersionNo");
                    DateOrder(row, "StartDate", "DueDate", "项目开始日期不能晚于截止日期");
                    break;
                case ProjectManagementExcelImportTemplate.TasksSheet:
                    Required(row, "StableId"); Required(row, "ProjectId"); Required(row, "TaskCode"); Required(row, "Title");
                    Allowed(row, "Status", ProjectManagementDomainRules.TaskStatuses); Allowed(row, "Priority", ["Low", "Medium", "High", "Urgent"]);
                    Date(row, "StartDate"); Date(row, "DueDate"); Percent(row, "ProgressPercent"); PositiveDecimal(row, "Weight"); NonNegativeInt(row, "SortOrder"); PositiveLong(row, "VersionNo");
                    DateOrder(row, "StartDate", "DueDate", "任务开始日期不能晚于截止日期");
                    break;
                case ProjectManagementExcelImportTemplate.MembersSheet:
                    Required(row, "StableId"); Required(row, "ProjectId"); Required(row, "UserId"); Allowed(row, "RoleCode", ProjectManagementDomainRules.ProjectRoles); Bool(row, "IsActive"); Date(row, "JoinedAt"); Date(row, "LeftAt"); PositiveLong(row, "VersionNo");
                    DateOrder(row, "JoinedAt", "LeftAt", "成员离开时间不能早于加入时间");
                    break;
            }
        }
    }

    private static List<ImportRow> ReadRows(IXLWorksheet sheet, string sheetName, IReadOnlyList<string> expectedColumns, List<RowIssue> globalIssues, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var firstRow = sheet.FirstRowUsed()?.RowNumber() ?? 0;
        if (firstRow == 0) return [];
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? firstRow;
        if (lastRow - firstRow > MaxRowsPerSheet) throw new ValidationException($"{sheetName} Sheet 行数不能超过 {MaxRowsPerSheet}");
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastColumn; column++)
        {
            var header = CellText(sheet.Cell(firstRow, column));
            if (header.Length == 0) continue;
            if (!expectedColumns.Contains(header, StringComparer.OrdinalIgnoreCase)) globalIssues.Add(new RowIssue(sheetName, firstRow, null, "UnknownHeader", $"不支持的列：{header}", "Error"));
            else if (!columns.TryAdd(header, column)) globalIssues.Add(new RowIssue(sheetName, firstRow, null, "DuplicateHeader", $"重复表头：{header}", "Error"));
        }

        foreach (var expected in expectedColumns)
            if (!columns.ContainsKey(expected)) globalIssues.Add(new RowIssue(sheetName, firstRow, null, "MissingHeader", $"缺少表头：{expected}", "Error"));
        if (globalIssues.Any(item => item.SheetName == sheetName && item.RowNumber == firstRow && item.Severity == "Error")) return [];

        var rows = new List<ImportRow>();
        for (var rowNumber = firstRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            CheckBudget(stopwatch, cancellationToken);
            if (Enumerable.Range(1, lastColumn).All(column => CellText(sheet.Cell(rowNumber, column)).Length == 0)) continue;
            var row = new ImportRow(sheetName, rowNumber);
            foreach (var expected in expectedColumns)
            {
                var cell = sheet.Cell(rowNumber, columns[expected]);
                if (cell.HasFormula) AddError(row, "FormulaNotAllowed", $"{expected} 不能使用公式");
                var value = CellText(cell);
                if (value.Length > MaxCellLength) AddError(row, "CellTooLong", $"{expected} 内容超过 {MaxCellLength} 个字符");
                row.Values[expected] = value;
            }

            var headerValues = expectedColumns.Select(item => row.Value(item)).ToArray();
            if (headerValues.SequenceEqual(expectedColumns, StringComparer.OrdinalIgnoreCase)) AddError(row, "DuplicateHeader", "数据区域中出现重复表头");
            rows.Add(row);
        }

        return rows;
    }

    private static int CountRows(IReadOnlyList<ImportRow> rows, ExistingData existingData, bool existing)
    {
        return rows.Count(item => !item.HasError && IsExisting(item, existingData) == existing);
    }

    private static bool IsExisting(ImportRow row, ExistingData existing) => row.SheetName switch
    {
        ProjectManagementExcelImportTemplate.ProjectsSheet => existing.Projects.Any(item => item.Id == row.StableId),
        ProjectManagementExcelImportTemplate.TasksSheet => existing.Tasks.Any(item => item.Id == row.StableId),
        ProjectManagementExcelImportTemplate.MembersSheet => existing.Members.Any(item => item.Id == row.StableId),
        _ => false
    };

    private static List<string> Ids(IReadOnlyList<ImportRow> rows, string sheetName) => rows.Where(item => item.SheetName == sheetName).Select(item => item.StableId).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct(StringComparer.Ordinal).ToList();

    private static IEnumerable<string> Tokens(string? value) => (value ?? string.Empty).Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CellText(IXLCell cell) => cell.GetString().Trim();

    private static void ValidateFile(IFormFile file)
    {
        if (file.Length <= 0) throw new ValidationException("Excel 文件不能为空");
        if (file.Length > MaxFileBytes) throw new ValidationException($"Excel 文件不能超过 {MaxFileBytes / 1024 / 1024} MB");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("仅支持 .xlsx Excel 文件");
    }

    private static void Required(ImportRow row, string field)
    {
        if (string.IsNullOrWhiteSpace(row.Value(field))) AddError(row, "Required", $"{field} 不能为空");
    }

    private static void Allowed(ImportRow row, string field, IEnumerable<string> allowed)
    {
        var value = row.Value(field);
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value, StringComparer.Ordinal)) AddError(row, "InvalidValue", $"{field} 值不受支持：{value}");
    }

    private static void Date(ImportRow row, string field)
    {
        var value = row.Value(field);
        if (!string.IsNullOrWhiteSpace(value) && !TryDate(value, out _)) AddError(row, "InvalidDate", $"{field} 不是有效日期");
    }

    private static void DateOrder(ImportRow row, string startField, string endField, string message)
    {
        if (TryDate(row.Value(startField), out var start) && TryDate(row.Value(endField), out var end) && start > end) AddError(row, "InvalidDateRange", message);
    }

    private static void Percent(ImportRow row, string field)
    {
        if (TryDecimal(row.Value(field), out var value) && (value < 0 || value > 100)) AddError(row, "InvalidPercent", $"{field} 必须在 0 到 100 之间");
        else if (!string.IsNullOrWhiteSpace(row.Value(field)) && !TryDecimal(row.Value(field), out _)) AddError(row, "InvalidNumber", $"{field} 不是有效数字");
    }

    private static void PositiveDecimal(ImportRow row, string field)
    {
        if (TryDecimal(row.Value(field), out var value) && value <= 0) AddError(row, "InvalidNumber", $"{field} 必须大于 0");
        else if (!string.IsNullOrWhiteSpace(row.Value(field)) && !TryDecimal(row.Value(field), out _)) AddError(row, "InvalidNumber", $"{field} 不是有效数字");
    }

    private static void NonNegativeInt(ImportRow row, string field)
    {
        if (TryDecimal(row.Value(field), out var value) && (value < 0 || decimal.Truncate(value) != value)) AddError(row, "InvalidNumber", $"{field} 必须是非负整数");
        else if (!string.IsNullOrWhiteSpace(row.Value(field)) && !TryDecimal(row.Value(field), out _)) AddError(row, "InvalidNumber", $"{field} 不是有效整数");
    }

    private static void PositiveLong(ImportRow row, string field)
    {
        if (TryDecimal(row.Value(field), out var value) && (value <= 0 || decimal.Truncate(value) != value)) AddError(row, "InvalidNumber", $"{field} 必须是正整数");
        else if (!string.IsNullOrWhiteSpace(row.Value(field)) && !TryDecimal(row.Value(field), out _)) AddError(row, "InvalidNumber", $"{field} 不是有效整数");
    }

    private static void Bool(ImportRow row, string field)
    {
        var value = row.Value(field);
        if (!string.IsNullOrWhiteSpace(value) && !bool.TryParse(value, out _) && value is not ("1" or "0" or "是" or "否")) AddError(row, "InvalidBoolean", $"{field} 不是有效布尔值");
    }

    private static bool TryDate(string? value, out DateTime result) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result) || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out result);

    private static bool TryDecimal(string? value, out decimal result) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result) || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);

    private static void AddEdge(Dictionary<string, List<string>> graph, string from, string to)
    {
        if (!graph.TryGetValue(from, out var next)) graph[from] = next = [];
        if (!next.Contains(to, StringComparer.Ordinal)) next.Add(to);
        graph.TryAdd(to, []);
    }

    private static void AddError(ImportRow row, string code, string message) => row.Issues.Add(new RowIssue(row.SheetName, row.RowNumber, row.StableId, code, message, "Error"));

    private static void CheckBudget(Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (stopwatch.Elapsed > MaxParseTime) throw new ValidationException("Excel 解析超过 30 秒上限，请拆分文件后重试");
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");

    private sealed class ImportRow(string sheetName, int rowNumber)
    {
        public string SheetName { get; } = sheetName;
        public int RowNumber { get; } = rowNumber;
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<RowIssue> Issues { get; } = [];
        public string? StableId => Value("StableId");
        public bool HasError => Issues.Any(item => item.Severity == "Error");
        public string? Value(string field) => Values.TryGetValue(field, out var value) ? value : null;
    }

    private sealed record RowIssue(string SheetName, int RowNumber, string? StableId, string Code, string Message, string Severity);

    private sealed record ExistingData(
        IReadOnlyList<ProjectManagementProjectEntity> Projects,
        IReadOnlyList<ProjectManagementTaskEntity> Tasks,
        IReadOnlyList<ProjectManagementProjectMemberEntity> Members,
        IReadOnlyList<ProjectManagementMilestoneEntity> Milestones,
        IReadOnlyList<ProjectManagementLabelEntity> Labels,
        IReadOnlyList<ProjectManagementTaskDependencyEntity> Dependencies);
}
