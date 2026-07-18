using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskBatchService
{
    Task<IReadOnlyList<ProjectManagementTaskResponse>> UpdateAsync(ProjectManagementTaskBatchUpdateRequest request, CancellationToken cancellationToken = default);
}
