using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementGanttScheduleService
{
    Task<ProjectManagementGanttScheduleBatchUpdateResponse> UpdateAsync(ProjectManagementGanttScheduleBatchUpdateRequest request, CancellationToken cancellationToken = default);
}
