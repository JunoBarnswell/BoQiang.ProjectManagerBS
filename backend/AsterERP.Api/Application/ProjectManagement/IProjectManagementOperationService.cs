using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementOperationService
{
    Task<ProjectManagementOperationResponse> GetAsync(string operationId, CancellationToken cancellationToken = default);
    Task<ProjectManagementOperationResponse> RequestCancellationAsync(string operationId, CancellationToken cancellationToken = default);
    Task<ProjectManagementOperationResponse> RunWorkspaceValidationAsync(CancellationToken cancellationToken = default);
}
