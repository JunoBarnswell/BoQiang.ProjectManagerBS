using System.Globalization;
using System.Text;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using ClosedXML.Excel;
using SqlSugar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementReportService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementReportService, ITransientDependency
{
    private const int MaxPageSize = 500;
    private const int MaxExportRows = 5000;

    private static readonly string[] Headers =
    [
        "ProjectCode", "ProjectName", "Status", "Priority", "OwnerUserId",
        "ProgressPercent", "TaskCount", "StartDate", "DueDate", "CreatedTime"
    ];

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
}
