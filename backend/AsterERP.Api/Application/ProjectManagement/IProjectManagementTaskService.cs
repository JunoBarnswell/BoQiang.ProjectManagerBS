using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskService
{
    Task<GridPageResult<ProjectManagementTaskResponse>> QueryAsync(ProjectManagementTaskQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskResponse> CreateAsync(string projectId, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskResponse> UpdateAsync(string id, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskResponse> MoveAsync(string id, ProjectManagementTaskMoveRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, long versionNo, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskResponse> RestoreAsync(string id, long versionNo, CancellationToken cancellationToken = default);
}
