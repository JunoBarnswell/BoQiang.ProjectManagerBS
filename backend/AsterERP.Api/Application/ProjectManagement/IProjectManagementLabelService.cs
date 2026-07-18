using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementLabelService
{
    Task<IReadOnlyList<ProjectManagementLabelResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementLabelResponse>> QueryPublicAsync(CancellationToken cancellationToken = default);
    Task<ProjectManagementLabelResponse> CreateAsync(string projectId, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementLabelResponse> CreatePublicAsync(ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementLabelResponse> UpdateAsync(string projectId, string id, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementLabelResponse> UpdatePublicAsync(string id, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default);
    Task DeletePublicAsync(string id, long versionNo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementTaskLabelResponse>> QueryTaskLabelsAsync(string taskId, CancellationToken cancellationToken = default);
    Task SetTaskLabelsAsync(string taskId, ProjectManagementTaskLabelSetRequest request, CancellationToken cancellationToken = default);
}
