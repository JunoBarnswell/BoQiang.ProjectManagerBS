using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementHomeQueryService
{
    Task<ProjectManagementHomeProjectsResponse> QueryProjectsAsync(
        ProjectManagementHomeQuery query,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementHomeSummaryResponse> QuerySummaryAsync(
        ProjectManagementHomeQuery query,
        CancellationToken cancellationToken = default);
}
