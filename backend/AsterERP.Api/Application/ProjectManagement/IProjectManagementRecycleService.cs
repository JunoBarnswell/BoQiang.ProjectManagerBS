using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementRecycleService
{
    Task<ProjectManagementRecycleResponse> QueryAsync(ProjectManagementRecycleQuery query, CancellationToken cancellationToken = default);
    Task RestoreProjectAsync(string id, ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken = default);
    Task RestoreTaskAsync(string id, ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken = default);
    Task PurgeProjectAsync(string id, long versionNo, CancellationToken cancellationToken = default);
}
