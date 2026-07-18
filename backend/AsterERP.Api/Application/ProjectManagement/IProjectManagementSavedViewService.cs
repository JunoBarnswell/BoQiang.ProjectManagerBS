using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementSavedViewService
{
    Task<IReadOnlyList<ProjectManagementSavedViewResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementSavedViewResponse> CreateAsync(string projectId, ProjectManagementSavedViewUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementSavedViewResponse> UpdateAsync(string projectId, string id, ProjectManagementSavedViewUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default);
}
