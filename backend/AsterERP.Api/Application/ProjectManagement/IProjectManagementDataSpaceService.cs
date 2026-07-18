using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementDataSpaceService
{
    Task<ProjectManagementDataSpaceSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
}
