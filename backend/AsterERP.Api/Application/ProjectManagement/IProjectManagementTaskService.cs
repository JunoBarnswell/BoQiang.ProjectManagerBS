using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskService
{
    Task<GridPageResult<ProjectManagementTaskListItemResponse>> QueryAsync(ProjectManagementTaskQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDetailResponse> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDetailResponse> CreateAsync(string projectId, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDetailResponse> UpdateAsync(string id, ProjectManagementTaskUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDetailResponse> MoveAsync(string id, ProjectManagementTaskMoveRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, long versionNo, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ProjectManagementTaskDeleteRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDetailResponse> RestoreAsync(string id, long versionNo, CancellationToken cancellationToken = default);
}
