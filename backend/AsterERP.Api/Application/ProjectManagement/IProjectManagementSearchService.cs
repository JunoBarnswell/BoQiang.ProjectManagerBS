using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementSearchService
{
    Task<ProjectManagementSearchResponse> SearchAsync(ProjectManagementSearchQuery query, CancellationToken cancellationToken = default);
}
