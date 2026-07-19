using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementDataSpaceImportService
{
    Task<ProjectManagementDataSpaceImportResponse> StartAsync(ProjectManagementDataSpaceImportRequest request, CancellationToken cancellationToken = default);
    Task ExecuteAsync(string operationId, CancellationToken cancellationToken = default);
}
