using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementMilestoneService
{
    Task<GridPageResult<ProjectManagementMilestoneResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementMilestoneResponse> CreateAsync(string projectId, ProjectManagementMilestoneUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementMilestoneResponse> UpdateAsync(string projectId, string id, ProjectManagementMilestoneUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default);
}
