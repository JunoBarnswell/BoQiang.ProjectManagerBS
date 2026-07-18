using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskParticipantService
{
    Task<IReadOnlyList<ProjectManagementTaskParticipantResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskParticipantResponse> AddAsync(string taskId, ProjectManagementTaskParticipantUpsertRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default);
}
