using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementProjectReminderService
{
    Task<IReadOnlyList<ProjectManagementProjectReminderResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementProjectReminderResponse> CreateAsync(string projectId, ProjectManagementProjectReminderCreateRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default);
}
