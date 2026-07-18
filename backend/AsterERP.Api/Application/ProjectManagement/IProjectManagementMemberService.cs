using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementMemberService
{
    Task<GridPageResult<ProjectManagementMemberResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementMemberResponse> AddAsync(string projectId, ProjectManagementMemberUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementMemberResponse> UpdateAsync(string projectId, string id, ProjectManagementMemberUpsertRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default);
}
