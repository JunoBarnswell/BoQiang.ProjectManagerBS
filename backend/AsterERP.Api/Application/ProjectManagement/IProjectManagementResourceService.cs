using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementResourceService
{
    Task<IReadOnlyList<ProjectManagementResourceResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementResourceResponse> CreateAsync(string projectId, ProjectManagementResourceUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementResourceResponse> UpdateAsync(string projectId, string id, ProjectManagementResourceUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default);
}
