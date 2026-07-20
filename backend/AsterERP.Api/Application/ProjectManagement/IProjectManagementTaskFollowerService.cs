using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskFollowerService
{
    Task<IReadOnlyList<ProjectManagementTaskFollowerResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskFollowerResponse> AddAsync(string taskId, ProjectManagementTaskFollowerUpsertRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string taskId, string userId, long versionNo, CancellationToken cancellationToken = default);
}
