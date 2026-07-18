using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementOverviewService
{
    Task<GridPageResult<ProjectManagementOverviewItem>> QueryAsync(
        ProjectManagementOverviewQuery query,
        CancellationToken cancellationToken = default);
}
