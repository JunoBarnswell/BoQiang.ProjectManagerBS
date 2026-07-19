using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskBatchExecutionService
{
    Task<ProjectManagementTaskBatchExecutionResult> ExecuteAsync(
        ProjectManagementTaskBatchUpdateRequest request,
        CancellationToken cancellationToken = default);
}
