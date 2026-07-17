using AsterERP.Contracts.System.QueryViews;

namespace AsterERP.Api.Application.System.QueryViews;

public interface IQueryViewExportService
{
    Task<QueryViewExportResponse> ExportAsync(string viewCode, QueryViewExportRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QueryViewExportTaskResponse>> GetTasksAsync(string? viewCode, CancellationToken cancellationToken = default);
}
