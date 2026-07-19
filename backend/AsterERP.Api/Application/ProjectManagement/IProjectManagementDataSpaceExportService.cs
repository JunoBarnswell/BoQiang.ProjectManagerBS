using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementDataSpaceExportService
{
    Task<ProjectManagementDataSpaceExportResponse> StartAsync(ProjectManagementDataSpaceExportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementDataSpaceExportResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task ExecuteAsync(string operationId, CancellationToken cancellationToken = default);
    Task<ProjectManagementDataSpaceExportDownload> DownloadAsync(string id, CancellationToken cancellationToken = default);
}
