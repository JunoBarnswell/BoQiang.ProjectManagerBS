using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementNotificationService : IProjectManagementNotificationPublisher
{
    Task<ProjectManagementNotificationPageResponse> QueryAsync(ProjectManagementNotificationQuery query, CancellationToken cancellationToken = default);
    Task MarkReadAsync(string id, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
    Task<ProjectManagementNotificationOpenResponse> OpenAsync(string id, CancellationToken cancellationToken = default);
}
