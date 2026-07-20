using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementProjectUpdateService
{
    Task<ProjectManagementActivityResponse> CreateAsync(string projectId, ProjectManagementProjectUpdateRequest request, CancellationToken cancellationToken = default);
}
