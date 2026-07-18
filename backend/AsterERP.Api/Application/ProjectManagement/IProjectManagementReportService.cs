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
}
