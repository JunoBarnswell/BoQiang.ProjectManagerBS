using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskReminderService
{
    Task<IReadOnlyList<ProjectManagementTaskReminderResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementTaskReminderResponse>> CreateAsync(string taskId, ProjectManagementTaskReminderCreateRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskReminderResponse> UpdateAsync(string taskId, string id, ProjectManagementTaskReminderUpdateRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default);
    Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default);
}
