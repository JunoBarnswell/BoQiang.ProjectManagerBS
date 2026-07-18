using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementRecycleService
{
    Task<ProjectManagementRecycleResponse> QueryAsync(ProjectManagementRecycleQuery query, CancellationToken cancellationToken = default);
    Task RestoreProjectAsync(string id, ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken = default);
    Task RestoreTaskAsync(string id, ProjectManagementRecycleRestoreRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementRecycleTaskPurgePreviewResponse> PreviewPurgeTaskAsync(string id, long versionNo, bool purgeDescendants = false, CancellationToken cancellationToken = default);
    Task PurgeTaskAsync(string id, ProjectManagementRecycleTaskPurgeRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementRecyclePurgePreviewResponse> PreviewPurgeProjectAsync(string id, long versionNo, CancellationToken cancellationToken = default);
    Task PurgeProjectAsync(string id, ProjectManagementRecyclePurgeRequest request, CancellationToken cancellationToken = default);
}
