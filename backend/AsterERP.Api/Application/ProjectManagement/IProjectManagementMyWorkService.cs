using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementMyWorkService
{
    Task<GridPageResult<ProjectManagementMyWorkItem>> QueryAsync(
        ProjectManagementMyWorkQuery query,
        CancellationToken cancellationToken = default);
}
