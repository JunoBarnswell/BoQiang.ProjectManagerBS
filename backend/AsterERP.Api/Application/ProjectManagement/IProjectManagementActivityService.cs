using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementActivityService : IProjectManagementActivityWriter
{
    Task<IReadOnlyList<ProjectManagementActivityResponse>> QueryAsync(string projectId, int limit = 100, CancellationToken cancellationToken = default);
}
