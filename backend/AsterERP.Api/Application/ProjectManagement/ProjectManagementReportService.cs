using System.Globalization;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
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
    IProjectManagementOperationWriter operationWriter,
    IBackgroundJobManager backgroundJobManager,
    IHostEnvironment environment) : IProjectManagementReportService, ITransientDependency
{
    private const int MaxPageSize = 500;
    private const int MaxExportRows = 5000;

    private static readonly string[] Headers =
    [
        "ProjectCode", "ProjectName", "Status", "Priority", "OwnerUserId",
        "ProgressPercent", "TaskCount", "StartDate", "DueDate", "CreatedTime"
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

    public async Task<ProjectManagementReportSnapshotStartResponse> StartSnapshotAsync(
        ProjectManagementReportSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var format = NormalizeFormat(request.Format);
        var operationId = Guid.NewGuid().ToString("N");
        var traceId = global::System.Diagnostics.Activity.Current?.Id ?? operationId;
        var impact = new SnapshotImpact(format, request.Query ?? new ProjectManagementReportQuery(), null, null, null, null);
        await operationWriter.CreatePendingAsync(operationId, "report.snapshot", JsonSerializer.Serialize(impact, SnapshotJsonOptions), traceId, cancellationToken);
        try
        {
            await backgroundJobManager.EnqueueAsync(new ProjectManagementOperationJobArgs(operationId, Tenant(), App(), UserId(), traceId));
        }
        catch (Exception exception)
        {
            await operationWriter.FailAsync(operationId, $"报表快照入队失败：{exception.Message}", CancellationToken.None);
            throw;
        }

        return new ProjectManagementReportSnapshotStartResponse(operationId);
    }

    public async Task ExecuteSnapshotAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var operation = await GetOwnedSnapshotOperationAsync(operationId, cancellationToken);
        var impact = DeserializeSnapshotImpact(operation.ImpactJson);
        var path = GetSnapshotPath(operation.Id, impact.Format);
        try
        {
            await operationWriter.StartAsync(operation.Id, "report.snapshot", operation.ImpactJson, operation.TraceId, cancellationToken);
            if (!await operationWriter.ReportProgressAsync(operation.Id, "正在读取授权范围内的数据", 15, cancellationToken)) return;
            var report = await GenerateFileAsync(impact.Format, impact.Query, cancellationToken);
            if (!await operationWriter.ReportProgressAsync(operation.Id, "正在写入快照文件", 80, cancellationToken)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, report.Content, cancellationToken);
            if (await operationWriter.IsCancellationRequestedAsync(operation.Id, cancellationToken))
            {
                TryDelete(path);
                await operationWriter.CancelAsync(operation.Id, cancellationToken);
                return;
            }

            var completed = impact with
            {
                FileName = report.FileName,
                ContentType = report.ContentType,
                RowCount = report.RowCount,
                DownloadReady = true
            };
            await operationWriter.CompleteWithImpactAsync(operation.Id, JsonSerializer.Serialize(completed, SnapshotJsonOptions), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryDelete(path);
            throw;
        }
        catch (Exception exception)
        {
            TryDelete(path);
            await operationWriter.FailAsync(operation.Id, $"报表快照生成失败：{exception.Message}", CancellationToken.None);
        }
    }

    public async Task<ProjectManagementReportFile> DownloadSnapshotAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var operation = await GetOwnedSnapshotOperationAsync(operationId, cancellationToken);
        if (!string.Equals(operation.Status, "Succeeded", StringComparison.Ordinal)) throw new ValidationException("报表快照尚未生成完成");
        var impact = DeserializeSnapshotImpact(operation.ImpactJson);
        if (impact is not { DownloadReady: true } || string.IsNullOrWhiteSpace(impact.FileName) || string.IsNullOrWhiteSpace(impact.ContentType))
            throw new ValidationException("报表快照产物不可用");

        var path = GetSnapshotPath(operation.Id, impact.Format);
        if (!File.Exists(path)) throw new NotFoundException("报表快照文件不存在", ErrorCodes.PlatformResourceNotFound);
        return new ProjectManagementReportFile(impact.FileName, impact.ContentType, await File.ReadAllBytesAsync(path, cancellationToken), impact.RowCount ?? 0);
    }

    private async Task<ProjectManagementReportFile> GenerateFileAsync(string format, ProjectManagementReportQuery query, CancellationToken cancellationToken) =>
        NormalizeFormat(format) switch
        {
            "csv" => await ExportCsvAsync(query, cancellationToken),
            "xlsx" => await ExportExcelAsync(query, cancellationToken),
            "pdf" => await ExportPdfAsync(query, cancellationToken),
            _ => throw new ValidationException("不支持的报表格式")
        };

    private async Task<ProjectManagementReportFile> ExportPdfAsync(ProjectManagementReportQuery query, CancellationToken cancellationToken)
    {
        var rows = await QueryRowsAsync(query, cancellationToken);
        return new ProjectManagementReportFile(
            $"project-management-report-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            "application/pdf",
            ProjectManagementPdfReportRenderer.Render(Headers, rows.Select(ToValues).ToList()),
            rows.Count);
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

        var page = await projects
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, new RefAsync<int>(), cancellationToken);
        if (page.Count == 0)
        {
            return [];
        }

        var projectIds = page.Select(item => item.Id).ToArray();
        var taskCounts = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => !item.IsDeleted && projectIds.Contains(item.ProjectId))
            .GroupBy(item => item.ProjectId)
            .Select(item => new ProjectTaskCount { ProjectId = item.ProjectId, TaskCount = SqlFunc.AggregateCount(item.Id) })
            .ToListAsync(cancellationToken);
        var countByProject = taskCounts.ToDictionary(item => item.ProjectId, item => item.TaskCount, StringComparer.Ordinal);

        return page.Select(item => new ProjectManagementReportRow(
            item.ProjectCode,
            item.ProjectName,
            item.Status,
            item.Priority,
            item.OwnerUserId,
            item.ProgressPercent,
            countByProject.GetValueOrDefault(item.Id),
            item.StartDate,
            item.DueDate,
            item.CreatedTime)).ToList();
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
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "data", "project-management-reports", Tenant(), App()));
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

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] ToValues(ProjectManagementReportRow row) =>
    [
        row.ProjectCode, row.ProjectName, row.Status, row.Priority, row.OwnerUserId,
        row.ProgressPercent.ToString(CultureInfo.InvariantCulture), row.TaskCount.ToString(CultureInfo.InvariantCulture),
        FormatDate(row.StartDate), FormatDate(row.DueDate), FormatDate(row.CreatedTime)
    ];

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

    private sealed class ProjectTaskCount
    {
        public string ProjectId { get; set; } = string.Empty;
        public int TaskCount { get; set; }
    }

    private sealed record SnapshotImpact(
        string Format,
        ProjectManagementReportQuery Query,
        string? FileName,
        string? ContentType,
        int? RowCount,
        bool? DownloadReady);
}
