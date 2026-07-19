using System.Globalization;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using ClosedXML.Excel;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementReportService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementOperationWriter? operationWriter = null,
    IBackgroundJobManager? backgroundJobManager = null,
    IHostEnvironment? environment = null,
    IProjectManagementTaskService? taskService = null) : IProjectManagementReportService, ITransientDependency
{
    private const int MaxPageSize = 500;
    private const int MaxExportRows = 5000;
    private const int TaskExportPageSize = 200;
    private const int MaxSnapshotProjects = 500;
    private const int MaxSnapshotComments = 200;
    private const int MaxSnapshotAttachments = 500;

    private static readonly string[] Headers =
    [
        "ProjectCode", "ProjectName", "Status", "Priority", "OwnerUserId",
        "ProgressPercent", "TaskCount", "StartDate", "DueDate", "CreatedTime",
        "EstimatedMinutes", "ActualMinutes"
    ];

    private static readonly string[] TaskHeaders =
    [
        "TaskId", "ProjectId", "MilestoneId", "ParentTaskId", "TaskCode", "Title", "Summary",
        "Status", "Priority", "AssigneeUserId", "StartDate", "DueDate", "ProgressPercent",
        "SortOrder", "Depth", "VersionNo", "BlockedByCount", "CanStart", "BlockedReason",
        "IsOverdue", "ActualStartAt", "ActualEndAt", "HasChildren"
    ];

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProjectManagementReportFile> ExportCsvAsync(
        ProjectManagementReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryRowsAsync(query, cancellationToken);
        var builder = new StringBuilder();
        AppendCsvRow(builder, Headers);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendCsvRow(builder, ToValues(row));
        }

        return new ProjectManagementReportFile(
            $"project-management-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
            "text/csv; charset=utf-8",
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray(),
            rows.Count);
    }

    public async Task<ProjectManagementReportFile> ExportExcelAsync(
        ProjectManagementReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryRowsAsync(query, cancellationToken);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("ProjectReport");
        for (var column = 0; column < Headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = Headers[column];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = ToValues(rows[rowIndex]);
            for (var column = 0; column < values.Length; column++)
            {
                var cell = worksheet.Cell(rowIndex + 2, column + 1);
                // ClosedXML treats a single leading apostrophe as an Excel quote
                // prefix and removes it when reading the cell back. Keep a
                // literal quote in the exported value while still preventing
                // formula evaluation.
                cell.Value = ExcelSafeForWorkbook(values[column]);
            }
        }

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns().AdjustToContents(1, 40);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ProjectManagementReportFile(
            $"project-management-report-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            stream.ToArray(),
            rows.Count);
    }

    public async Task<ProjectManagementReportFile> ExportTasksCsvAsync(
        ProjectManagementTaskQuery query,
        CancellationToken cancellationToken = default)
    {
        var service = taskService ?? throw new InvalidOperationException("任务报表服务未注册");
        query = ProjectManagementTaskQueryProtocol.Normalize(query);
        var builder = new StringBuilder();
        AppendCsvRow(builder, TaskHeaders);
        var totalWritten = 0;
        var pageIndex = 1;
        long total = 0;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await service.QueryAsync(query with { PageIndex = pageIndex, PageSize = TaskExportPageSize }, cancellationToken);
            total = page.Total;
            if (total > MaxExportRows)
                throw new ValidationException($"当前筛选结果超过单次导出上限 {MaxExportRows} 行，请缩小筛选条件");

            foreach (var row in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendCsvRow(builder, ToTaskValues(row));
                totalWritten++;
            }

            if (page.Items.Count == 0 || totalWritten >= total) break;
            pageIndex++;
        } while (true);

        return new ProjectManagementReportFile(
            BuildTaskCsvFileName(query.ProjectId),
            "text/csv; charset=utf-8",
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray(),
            totalWritten);
    }

    public async Task<ProjectManagementReportSnapshotStartResponse> StartSnapshotAsync(
        ProjectManagementReportSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var format = NormalizeFormat(request.Format);
        var options = NormalizeOptions(request.Options);
        var operationId = Guid.NewGuid().ToString("N");
        var traceId = global::System.Diagnostics.Activity.Current?.Id ?? operationId;
        var expiresAt = DateTime.UtcNow.AddHours(options.RetentionHours);
        var impact = new SnapshotImpact(format, NormalizeReportQuery(request.Query), options, expiresAt, null, null, null, null, null);
        var writer = operationWriter ?? throw new InvalidOperationException("报表快照任务写入器未注册");
        var jobManager = backgroundJobManager ?? throw new InvalidOperationException("报表快照后台队列未注册");
        await writer.CreatePendingAsync(operationId, "report.snapshot", JsonSerializer.Serialize(impact, SnapshotJsonOptions), traceId, cancellationToken);
        try
        {
            await jobManager.EnqueueAsync(new ProjectManagementOperationJobArgs(operationId, Tenant(), App(), UserId(), traceId));
        }
        catch (Exception exception)
        {
            await writer.FailAsync(operationId, $"报表快照入队失败：{exception.Message}", CancellationToken.None);
            throw;
        }

        return new ProjectManagementReportSnapshotStartResponse(operationId, traceId, expiresAt);
    }

    public async Task ExecuteSnapshotAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var writer = operationWriter ?? throw new InvalidOperationException("报表快照任务写入器未注册");
        var operation = await GetOwnedSnapshotOperationAsync(operationId, cancellationToken);
        var impact = DeserializeSnapshotImpact(operation.ImpactJson);
        var path = GetSnapshotPath(operation.Id, impact.Format);
        try
        {
            await writer.StartAsync(operation.Id, "report.snapshot", operation.ImpactJson, operation.TraceId, cancellationToken);
            if (!await writer.ReportProgressAsync(operation.Id, "正在读取授权范围内的数据", 15, cancellationToken)) return;
            if (DateTime.UtcNow >= impact.ExpiresAt) throw new ValidationException("报表快照已超过有效期，请重新生成");
            var report = await GenerateFileAsync(impact.Format, impact.Query, impact.Options, cancellationToken);
            if (!await writer.ReportProgressAsync(operation.Id, "正在写入快照文件", 80, cancellationToken)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, report.Content, cancellationToken);
            if (await writer.IsCancellationRequestedAsync(operation.Id, cancellationToken))
            {
                TryDelete(path);
                await writer.CancelAsync(operation.Id, cancellationToken);
                return;
            }

            var completed = impact with
            {
                FileName = report.FileName,
                ContentType = report.ContentType,
                RowCount = report.RowCount,
                DownloadReady = true,
                CompletedAt = DateTime.UtcNow
            };
            await writer.CompleteWithImpactAsync(operation.Id, JsonSerializer.Serialize(completed, SnapshotJsonOptions), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryDelete(path);
            throw;
        }
        catch (Exception exception)
        {
            TryDelete(path);
            await writer.FailAsync(operation.Id, $"报表快照生成失败：{exception.Message}", CancellationToken.None);
        }
    }

    public async Task<ProjectManagementReportFile> DownloadSnapshotAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var operation = await GetOwnedSnapshotOperationAsync(operationId, cancellationToken);
        if (!string.Equals(operation.Status, "Succeeded", StringComparison.Ordinal)) throw new ValidationException("报表快照尚未生成完成");
        var impact = DeserializeSnapshotImpact(operation.ImpactJson);
        if (impact is not { DownloadReady: true } || string.IsNullOrWhiteSpace(impact.FileName) || string.IsNullOrWhiteSpace(impact.ContentType))
            throw new ValidationException("报表快照产物不可用");
        if (DateTime.UtcNow >= impact.ExpiresAt)
        {
            TryDelete(GetSnapshotPath(operation.Id, impact.Format));
            throw new ValidationException("报表快照已过期，请重新生成");
        }

        var path = GetSnapshotPath(operation.Id, impact.Format);
        if (!File.Exists(path)) throw new NotFoundException("报表快照文件不存在", ErrorCodes.PlatformResourceNotFound);
        return new ProjectManagementReportFile(impact.FileName, impact.ContentType, await File.ReadAllBytesAsync(path, cancellationToken), impact.RowCount ?? 0);
    }

    public async Task<ProjectManagementReportSnapshotStartResponse> RetrySnapshotAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var writer = operationWriter ?? throw new InvalidOperationException("报表快照任务写入器未注册");
        var operation = await GetOwnedSnapshotOperationAsync(operationId, cancellationToken);
        if (!string.Equals(operation.Status, "Failed", StringComparison.Ordinal))
            throw new ValidationException("只有失败的报表快照任务可以重试");

        var previous = DeserializeSnapshotImpact(operation.ImpactJson);
        var retryId = Guid.NewGuid().ToString("N");
        var traceId = global::System.Diagnostics.Activity.Current?.Id ?? retryId;
        var expiresAt = DateTime.UtcNow.AddHours(previous.Options.RetentionHours);
        var impact = previous with { ExpiresAt = expiresAt, FileName = null, ContentType = null, RowCount = null, DownloadReady = false, CompletedAt = null };
        await writer.CreatePendingAsync(retryId, "report.snapshot", JsonSerializer.Serialize(impact, SnapshotJsonOptions), traceId, cancellationToken);
        try
        {
            await (backgroundJobManager ?? throw new InvalidOperationException("报表快照后台队列未注册"))
                .EnqueueAsync(new ProjectManagementOperationJobArgs(retryId, Tenant(), App(), UserId(), traceId));
        }
        catch (Exception exception)
        {
            await writer.FailAsync(retryId, $"报表快照重试入队失败：{exception.Message}", CancellationToken.None);
            throw;
        }
        return new ProjectManagementReportSnapshotStartResponse(retryId, traceId, expiresAt);
    }

    private async Task<ProjectManagementReportFile> GenerateFileAsync(string format, ProjectManagementReportQuery query, ProjectManagementReportSnapshotOptions options, CancellationToken cancellationToken) =>
        NormalizeFormat(format) switch
        {
            "csv" => await ExportCsvAsync(query, cancellationToken),
            "xlsx" => await ExportExcelAsync(query, cancellationToken),
            "pdf" => await ExportPdfAsync(query, options, cancellationToken),
            _ => throw new ValidationException("不支持的报表格式")
        };

    private async Task<ProjectManagementReportFile> ExportPdfAsync(ProjectManagementReportQuery query, ProjectManagementReportSnapshotOptions options, CancellationToken cancellationToken)
    {
        var document = await QueryPdfDocumentAsync(query, options, cancellationToken);
        return new ProjectManagementReportFile(
            $"project-management-report-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            "application/pdf",
            ProjectManagementPdfReportRenderer.Render(document),
            document.Tasks.Count);
    }

    private async Task<IReadOnlyList<ProjectManagementReportRow>> QueryRowsAsync(
        ProjectManagementReportQuery query,
        CancellationToken cancellationToken)
    {
        RequireTenantId();
        RequireAppCode();
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var keyword = NormalizeOptional(query.Keyword);
        var status = NormalizeOptional(query.Status);
        var labelFilter = ProjectManagementTaskLabelFilterQuery.Normalize(query.LabelFilter);
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetCurrentDb();

        var projects = db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => !item.IsDeleted);
        if (keyword is not null)
        {
            projects = projects.Where(item =>
                item.ProjectCode.Contains(keyword) ||
                item.ProjectName.Contains(keyword) ||
                (item.Description != null && item.Description.Contains(keyword)));
        }

        if (status is not null)
        {
            projects = projects.Where(item => item.Status == status);
        }
        projects = ProjectManagementTaskLabelFilterQuery.ApplyToProjects(projects, labelFilter, tenantId, appCode);

        var page = await projects
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, new RefAsync<int>(), cancellationToken);
        if (page.Count == 0)
        {
            return [];
        }

        var projectIds = page.Select(item => item.Id).ToArray();
        var taskCountsQuery = db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => !item.IsDeleted && projectIds.Contains(item.ProjectId));
        taskCountsQuery = ProjectManagementTaskLabelFilterQuery.ApplyToTasks(taskCountsQuery, labelFilter, tenantId, appCode);
        var taskSummaries = await taskCountsQuery
            .GroupBy(item => item.ProjectId)
            .Select(item => new ProjectTaskSummary
            {
                ProjectId = item.ProjectId,
                TaskCount = SqlFunc.AggregateCount(item.Id),
                EstimatedMinutes = SqlFunc.AggregateSum(item.EstimateMinutes ?? 0),
                ActualMinutes = SqlFunc.AggregateSum(item.ActualMinutes)
            })
            .ToListAsync(cancellationToken);
        var summariesByProject = taskSummaries.ToDictionary(item => item.ProjectId, StringComparer.Ordinal);

        return page.Select(item =>
        {
            summariesByProject.TryGetValue(item.Id, out var summary);
            return new ProjectManagementReportRow(
                item.ProjectCode,
                item.ProjectName,
                item.Status,
                item.Priority,
                item.OwnerUserId,
                item.ProgressPercent,
                summary?.TaskCount ?? 0,
                item.StartDate,
                item.DueDate,
                item.CreatedTime,
                summary?.EstimatedMinutes ?? 0,
                summary?.ActualMinutes ?? 0);
        }).ToList();
    }

    private async Task<ProjectManagementReportPdfDocument> QueryPdfDocumentAsync(
        ProjectManagementReportQuery query,
        ProjectManagementReportSnapshotOptions options,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetCurrentDb();
        var normalizedQuery = NormalizeReportQuery(query);
        var keyword = NormalizeOptional(normalizedQuery.Keyword);
        var status = NormalizeOptional(normalizedQuery.Status);
        var labelFilter = ProjectManagementTaskLabelFilterQuery.Normalize(normalizedQuery.LabelFilter);
        var projectsQuery = db.Queryable<ProjectManagementProjectEntity>();
        if (!options.IncludeDeleted) projectsQuery = projectsQuery.Where(item => !item.IsDeleted);
        if (keyword is not null)
            projectsQuery = projectsQuery.Where(item => item.ProjectCode.Contains(keyword) || item.ProjectName.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        if (status is not null) projectsQuery = projectsQuery.Where(item => item.Status == status);
        projectsQuery = ProjectManagementTaskLabelFilterQuery.ApplyToProjects(projectsQuery, labelFilter, tenantId, appCode);
        var projects = await projectsQuery
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(MaxSnapshotProjects + 1)
            .ToListAsync(cancellationToken);
        if (projects.Count > MaxSnapshotProjects)
            throw new ValidationException($"PDF 报告最多包含 {MaxSnapshotProjects} 个项目，请缩小筛选条件");

        var projectIds = projects.Select(item => item.Id).ToArray();
        var tasksQuery = db.Queryable<ProjectManagementTaskEntity>().Where(item => projectIds.Contains(item.ProjectId));
        if (!options.IncludeDeleted) tasksQuery = tasksQuery.Where(item => !item.IsDeleted);
        if (!options.IncludeCompleted) tasksQuery = tasksQuery.Where(item => item.Status != ProjectManagementDomainRules.TaskDone && item.Status != ProjectManagementDomainRules.TaskCancelled);
        if (keyword is not null) tasksQuery = tasksQuery.Where(item => item.TaskCode.Contains(keyword) || item.Title.Contains(keyword) || (item.Summary != null && item.Summary.Contains(keyword)));
        tasksQuery = ProjectManagementTaskLabelFilterQuery.ApplyToTasks(tasksQuery, labelFilter, tenantId, appCode);
        var tasks = await tasksQuery.OrderBy(item => item.ProjectId).OrderBy(item => item.Depth).OrderBy(item => item.SortOrder).Take(options.MaxTaskRows + 1).ToListAsync(cancellationToken);
        if (tasks.Count > options.MaxTaskRows)
            throw new ValidationException($"PDF 报告任务数超过上限 {options.MaxTaskRows}，请缩小筛选条件或提高任务上限");

        var taskIds = tasks.Select(item => item.Id).ToArray();
        var milestonesQuery = db.Queryable<ProjectManagementMilestoneEntity>().Where(item => projectIds.Contains(item.ProjectId));
        if (!options.IncludeDeleted) milestonesQuery = milestonesQuery.Where(item => !item.IsDeleted);
        var milestones = await milestonesQuery.OrderBy(item => item.ProjectId).OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
        var dependencies = taskIds.Length == 0 ? [] : await db.Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => taskIds.Contains(item.PredecessorTaskId) && taskIds.Contains(item.SuccessorTaskId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var taskRows = tasks.Select(item => new ProjectManagementReportPdfTask(item.Id, item.ProjectId, item.TaskCode, item.Title, item.Status, item.Priority, item.AssigneeUserId, item.StartDate, item.DueDate, item.ProgressPercent, item.EstimateMinutes ?? 0, item.ActualMinutes, item.Depth, !string.IsNullOrWhiteSpace(item.BlockedReason) || item.Status == "Blocked", item.IsDeleted)).ToList();
        var taskById = taskRows.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var summaryByProject = taskRows.GroupBy(item => item.ProjectId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => new ProjectTaskSummary { ProjectId = group.Key, TaskCount = group.Count(), EstimatedMinutes = group.Sum(item => item.EstimateMinutes), ActualMinutes = group.Sum(item => item.ActualMinutes) }, StringComparer.Ordinal);
        var projectRows = projects.Select(item =>
        {
            summaryByProject.TryGetValue(item.Id, out var summary);
            return new ProjectManagementReportRow(item.ProjectCode, item.ProjectName, item.Status, item.Priority, item.OwnerUserId, item.ProgressPercent, summary?.TaskCount ?? 0, item.StartDate, item.DueDate, item.CreatedTime, summary?.EstimatedMinutes ?? 0, summary?.ActualMinutes ?? 0);
        }).ToList();

        var comments = new List<string>();
        if (options.IncludeCommentSummary && taskIds.Length > 0)
        {
            var commentRows = await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => taskIds.Contains(item.TaskId) && !item.IsDeleted).OrderBy(item => item.CreatedTime, OrderByType.Desc).Take(MaxSnapshotComments).ToListAsync(cancellationToken);
            comments.AddRange(commentRows.Select(item => $"{taskById.GetValueOrDefault(item.TaskId)?.TaskCode ?? item.TaskId}: {SafeReportText(item.Markdown, 180)}"));
        }
        var attachments = new List<string>();
        if (options.IncludeAttachmentList && taskIds.Length > 0)
        {
            var attachmentRows = await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => taskIds.Contains(item.TaskId) && !item.IsDeleted).OrderBy(item => item.CreatedTime, OrderByType.Desc).Take(MaxSnapshotAttachments).ToListAsync(cancellationToken);
            attachments.AddRange(attachmentRows.Select(item => $"{taskById.GetValueOrDefault(item.TaskId)?.TaskCode ?? item.TaskId}: {SafeReportText(item.FileName, 140)} ({item.FileSize} bytes)"));
        }

        var now = DateTime.UtcNow;
        var criticalPath = CalculateCriticalPath(taskRows, dependencies);
        return new ProjectManagementReportPdfDocument(
            now, tenantId, appCode, UserId(), projectRows,
            milestones.Select(item => new ProjectManagementReportPdfMilestone(item.ProjectId, item.MilestoneName, item.Status, item.ProgressPercent, item.DueDate)).ToList(),
            taskRows,
            taskRows.GroupBy(item => item.Status, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            taskRows.Sum(item => item.EstimateMinutes), taskRows.Sum(item => item.ActualMinutes),
            taskRows.Count(item => item.DueDate.HasValue && item.DueDate.Value >= now),
            taskRows.Count(item => item.DueDate.HasValue && item.DueDate.Value < now && item.Status != ProjectManagementDomainRules.TaskDone && item.Status != ProjectManagementDomainRules.TaskCancelled),
            taskRows.Count(item => item.IsBlocked), criticalPath, comments, attachments, options.IncludeGanttSnapshot, options.IncludeDeleted);
    }

    private static IReadOnlyList<ProjectManagementReportPdfTask> CalculateCriticalPath(
        IReadOnlyList<ProjectManagementReportPdfTask> tasks,
        IReadOnlyList<ProjectManagementTaskDependencyEntity> dependencies)
    {
        var byId = tasks.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var predecessors = dependencies.GroupBy(item => item.SuccessorTaskId).ToDictionary(group => group.Key, group => group.Select(item => item.PredecessorTaskId).Where(byId.ContainsKey).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        var memo = new Dictionary<string, IReadOnlyList<ProjectManagementReportPdfTask>>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<ProjectManagementReportPdfTask> Path(string id)
        {
            if (memo.TryGetValue(id, out var cached)) return cached;
            if (!visiting.Add(id)) return [byId[id]];
            var best = new List<ProjectManagementReportPdfTask>();
            foreach (var predecessor in predecessors.GetValueOrDefault(id) ?? [])
            {
                var candidate = Path(predecessor);
                if (candidate.Count > best.Count) best = candidate.ToList();
            }
            visiting.Remove(id);
            best.Add(byId[id]);
            memo[id] = best;
            return best;
        }

        return tasks.Count == 0 ? [] : tasks.Select(item => Path(item.Id)).OrderByDescending(item => item.Count).ThenByDescending(item => item.Sum(task => task.EstimateMinutes)).First();
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");

    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");

    private string Tenant() => RequireTenantId();
    private string App() => RequireAppCode();
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");

    private async Task<ProjectManagementOperationEntity> GetOwnedSnapshotOperationAsync(string operationId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == operationId.Trim() && item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() && item.OperationType == "report.snapshot" && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
        ?? throw new NotFoundException("报表快照任务不存在或无权访问", ErrorCodes.PlatformResourceNotFound);

    private SnapshotImpact DeserializeSnapshotImpact(string json)
    {
        try { return JsonSerializer.Deserialize<SnapshotImpact>(json, SnapshotJsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw new ValidationException("报表快照任务数据损坏"); }
    }

    private string GetSnapshotPath(string operationId, string format)
    {
        var extension = NormalizeFormat(format);
        var hostEnvironment = environment ?? throw new InvalidOperationException("报表快照宿主环境未注册");
        var root = Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, "data", "project-management-reports", Tenant(), App()));
        var path = Path.GetFullPath(Path.Combine(root, $"{operationId}.{extension}"));
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ValidationException("报表快照路径不合法");
        return path;
    }

    private static string NormalizeFormat(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "csv" => "csv",
        "xlsx" or "excel" => "xlsx",
        "pdf" => "pdf",
        _ => throw new ValidationException("报表格式仅支持 CSV、Excel 或 PDF")
    };

    private static ProjectManagementReportQuery NormalizeReportQuery(ProjectManagementReportQuery? query) =>
        (query ?? new ProjectManagementReportQuery()) with
        {
            PageIndex = Math.Max(query?.PageIndex ?? 1, 1),
            PageSize = Math.Clamp(query?.PageSize ?? 100, 1, MaxPageSize),
            Keyword = NormalizeOptional(query?.Keyword),
            Status = NormalizeOptional(query?.Status)
        };

    private static ProjectManagementReportSnapshotOptions NormalizeOptions(ProjectManagementReportSnapshotOptions? options)
    {
        options ??= new ProjectManagementReportSnapshotOptions();
        return options with
        {
            MaxTaskRows = Math.Clamp(options.MaxTaskRows, 1, 10000),
            RetentionHours = Math.Clamp(options.RetentionHours, 1, 168)
        };
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SafeReportText(string? value, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    private static string[] ToValues(ProjectManagementReportRow row) =>
    [
        row.ProjectCode, row.ProjectName, row.Status, row.Priority, row.OwnerUserId,
        row.ProgressPercent.ToString(CultureInfo.InvariantCulture), row.TaskCount.ToString(CultureInfo.InvariantCulture),
        FormatDate(row.StartDate), FormatDate(row.DueDate), FormatDate(row.CreatedTime),
        row.EstimatedMinutes.ToString(CultureInfo.InvariantCulture), row.ActualMinutes.ToString(CultureInfo.InvariantCulture)
    ];

    private static string[] ToTaskValues(ProjectManagementTaskListItemResponse row) =>
    [
        row.Id, row.ProjectId, row.MilestoneId ?? string.Empty, row.ParentTaskId ?? string.Empty, row.TaskCode,
        row.Title, row.Summary ?? string.Empty, row.Status, row.Priority, row.AssigneeUserId ?? string.Empty,
        FormatDate(row.StartDate), FormatDate(row.DueDate), row.ProgressPercent.ToString(CultureInfo.InvariantCulture),
        row.SortOrder.ToString(CultureInfo.InvariantCulture), row.Depth.ToString(CultureInfo.InvariantCulture),
        row.VersionNo.ToString(CultureInfo.InvariantCulture), row.BlockedByCount.ToString(CultureInfo.InvariantCulture),
        row.CanStart ? "true" : "false", row.BlockedReason ?? string.Empty, row.IsOverdue ? "true" : "false",
        FormatDate(row.ActualStartAt), FormatDate(row.ActualEndAt), row.HasChildren ? "true" : "false"
    ];

    private static string BuildTaskCsvFileName(string projectId)
    {
        var safeProjectId = new string(projectId.Where(char.IsLetterOrDigit).Take(48).ToArray());
        if (safeProjectId.Length == 0) safeProjectId = "project";
        return $"project-management-tasks-{safeProjectId}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
    }

    private static string FormatDate(DateTime? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private static void AppendCsvRow(StringBuilder builder, IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0) builder.Append(',');
            var value = ExcelSafe(values[index]);
            builder.Append('"').Append(value.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
        }

        builder.AppendLine();
    }

    private static string ExcelSafe(string value)
    {
        if (value.Length == 0) return value;
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ? "'" + value : value;
    }

    private static string ExcelSafeForWorkbook(string value)
    {
        if (value.Length == 0) return value;
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ? "''" + value : value;
    }

    private sealed class ProjectTaskSummary
    {
        public string ProjectId { get; set; } = string.Empty;
        public int TaskCount { get; set; }
        public int EstimatedMinutes { get; set; }
        public int ActualMinutes { get; set; }
    }

    private sealed record SnapshotImpact(
        string Format,
        ProjectManagementReportQuery Query,
        ProjectManagementReportSnapshotOptions Options,
        DateTime ExpiresAt,
        string? FileName,
        string? ContentType,
        int? RowCount,
        bool? DownloadReady,
        DateTime? CompletedAt);
}
