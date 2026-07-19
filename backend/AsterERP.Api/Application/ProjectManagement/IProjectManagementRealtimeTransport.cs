using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementRealtimeTransport
{
    Task PublishNotificationCreatedAsync(string tenantId, string appCode, string recipientUserId, string notificationId, CancellationToken cancellationToken = default);

    Task PublishOperationProgressAsync(string tenantId, string appCode, string userId, ProjectManagementOperationProgressEvent progressEvent, CancellationToken cancellationToken = default);

    Task PublishInvalidationAsync(string tenantId, string appCode, string projectId, ProjectManagementRealtimeEvent invalidation, CancellationToken cancellationToken = default);
    Task PublishHomeInvalidationAsync(string tenantId, string appCode, IReadOnlyCollection<string> userIds, ProjectManagementRealtimeEvent invalidation, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task RevokeProjectAccessAsync(string tenantId, string appCode, string projectId, string connectionId, CancellationToken cancellationToken = default);
}
