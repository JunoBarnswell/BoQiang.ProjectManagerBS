using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementReportService
{
    Task<ProjectManagementReportFile> ExportCsvAsync(
        ProjectManagementReportQuery query,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementReportFile> ExportExcelAsync(
        ProjectManagementReportQuery query,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementReportFile> ExportTasksCsvAsync(
        ProjectManagementTaskQuery query,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementReportSnapshotStartResponse> StartSnapshotAsync(
        ProjectManagementReportSnapshotRequest request,
        CancellationToken cancellationToken = default);

    Task ExecuteSnapshotAsync(string operationId, CancellationToken cancellationToken = default);

    Task<ProjectManagementReportFile> DownloadSnapshotAsync(string operationId, CancellationToken cancellationToken = default);
}
