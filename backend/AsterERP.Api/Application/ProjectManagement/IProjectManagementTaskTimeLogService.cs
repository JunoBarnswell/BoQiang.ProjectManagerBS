using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskTimeLogService
{
    Task<IReadOnlyList<ProjectManagementTaskTimeLogResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskTimeLogResponse> CreateAsync(string taskId, ProjectManagementTaskTimeLogUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskTimeLogResponse> UpdateAsync(string taskId, string id, ProjectManagementTaskTimeLogUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementTaskWorkloadResponse>> QueryWorkloadAsync(ProjectManagementTaskWorkloadQuery query, CancellationToken cancellationToken = default);
}
