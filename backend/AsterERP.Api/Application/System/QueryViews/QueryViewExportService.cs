using ClosedXML.Excel;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.QueryViews;
using AsterERP.Api.Modules.System.QueryViews;
using SqlSugar;

namespace AsterERP.Api.Application.System.QueryViews;

public sealed class QueryViewExportService(
    ICurrentUser currentUser,
    IWorkspaceDatabaseAccessor databaseAccessor,
    IQueryViewRuntimeService runtimeService) : IQueryViewExportService
{
    private const int SyncExportLimit = 5000;

    public async Task<QueryViewExportResponse> ExportAsync(string viewCode, QueryViewExportRequest request, CancellationToken cancellationToken = default)
    {
        var taskNo = $"QVE{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var exportName = $"{viewCode}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        var task = new SystemQueryViewExportTaskEntity
        {
            TaskNo = taskNo,
            ViewCode = viewCode,
            ExportName = exportName,
            Status = "running",
            CreatedBy = currentUser.GetAsterErpUserId()
        };
        await databaseAccessor.GetCurrentDb().Insertable(task).ExecuteCommandAsync(cancellationToken);

        try
        {
            var conditions = request.Conditions.ToList();
            if (string.Equals(request.ExportMode, "selected", StringComparison.OrdinalIgnoreCase))
            {
                conditions.Add(new QueryViewQueryCondition("id", "in", request.SelectedRowIds, null));
            }

            var queryResult = await runtimeService.QueryAsync(
                viewCode,
                new QueryViewQueryRequest(1, SyncExportLimit, conditions, request.Sorts),
                cancellationToken);

            task.TotalCount = queryResult.Total;
            if (queryResult.Total > SyncExportLimit)
            {
                task.Status = "waiting";
                task.UpdatedTime = DateTime.UtcNow;
                await databaseAccessor.GetCurrentDb().Updateable(task).ExecuteCommandAsync(cancellationToken);
                return new QueryViewExportResponse(taskNo, "waiting", exportName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", null, queryResult.Total);
            }

            var columns = ResolveExportColumns(request.Columns, queryResult.Rows);
            var content = BuildWorkbook(exportName, columns, queryResult.Rows);
            task.Status = "success";
            task.FinishedTime = DateTime.UtcNow;
            task.UpdatedTime = DateTime.UtcNow;
            await databaseAccessor.GetCurrentDb().Updateable(task).ExecuteCommandAsync(cancellationToken);

            return new QueryViewExportResponse(
                taskNo,
                "success",
                exportName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Convert.ToBase64String(content),
                queryResult.Total);
        }
        catch (Exception ex) when (ex is not ValidationException)
        {
            task.Status = "failed";
            task.ErrorMessage = ex.Message;
            task.FinishedTime = DateTime.UtcNow;
            task.UpdatedTime = DateTime.UtcNow;
            await databaseAccessor.GetCurrentDb().Updateable(task).ExecuteCommandAsync(cancellationToken);
            throw new ValidationException($"导出失败: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<QueryViewExportTaskResponse>> GetTasksAsync(string? viewCode, CancellationToken cancellationToken = default)
    {
        var query = databaseAccessor.GetCurrentDb().Queryable<SystemQueryViewExportTaskEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(viewCode))
        {
            query = query.Where(item => item.ViewCode == viewCode.Trim());
        }

        var tasks = await query.OrderBy(item => item.CreatedTime, OrderByType.Desc).ToListAsync(cancellationToken);
        return tasks.Select(item => new QueryViewExportTaskResponse(
            item.Id,
            item.TaskNo,
            item.ViewCode,
            item.ExportName,
            item.Status,
            item.FileUrl,
            item.TotalCount,
            item.CreatedTime,
            item.FinishedTime,
            item.ErrorMessage)).ToList();
    }

    private static IReadOnlyList<string> ResolveExportColumns(
        IReadOnlyList<string> requestedColumns,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        if (requestedColumns.Count > 0)
        {
            return requestedColumns.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return rows.FirstOrDefault()?.Keys.Where(item => !string.Equals(item, "id", StringComparison.OrdinalIgnoreCase)).ToList() ?? [];
    }

    private static byte[] BuildWorkbook(
        string sheetName,
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("export");
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = columns[columnIndex];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                rows[rowIndex].TryGetValue(columns[columnIndex], out var value);
                worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = value?.ToString() ?? string.Empty;
            }
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

