using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementProjectService
{
    Task<GridPageResult<ProjectManagementProjectResponse>> QueryAsync(
        ProjectManagementProjectQuery query,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementProjectResponse> CreateAsync(
        ProjectManagementProjectUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementProjectResponse> UpdateAsync(
        string id,
        ProjectManagementProjectUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementProjectResponse> RestoreAsync(
        string id,
        long versionNo,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, long versionNo, CancellationToken cancellationToken = default);
}
