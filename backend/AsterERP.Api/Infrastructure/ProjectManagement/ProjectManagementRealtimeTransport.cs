using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.SignalR;
using AsterERP.Contracts.ProjectManagement;
using Microsoft.AspNetCore.SignalR;

namespace AsterERP.Api.Infrastructure.ProjectManagement;

public sealed class ProjectManagementRealtimeTransport(IHubContext<SystemNotificationHub> hubContext) : IProjectManagementRealtimeTransport
{
    public Task PublishNotificationCreatedAsync(string tenantId, string appCode, string recipientUserId, string notificationId, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(SystemNotificationHub.BuildProjectManagementNotificationUserGroupName(tenantId, appCode, recipientUserId))
            .SendAsync("ProjectManagementNotificationCreated", new ProjectManagementNotificationCreatedEvent(notificationId), cancellationToken);

    public Task PublishOperationProgressAsync(string tenantId, string appCode, string userId, ProjectManagementOperationProgressEvent progressEvent, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(SystemNotificationHub.BuildProjectManagementOperationUserGroupName(tenantId, appCode, userId))
            .SendAsync("ProjectManagementOperationProgressUpdated", progressEvent, cancellationToken);

    public Task PublishInvalidationAsync(string tenantId, string appCode, string projectId, ProjectManagementRealtimeEvent invalidation, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(SystemNotificationHub.BuildProjectManagementProjectGroupName(tenantId, appCode, projectId))
            .SendAsync("ProjectManagementInvalidated", invalidation, cancellationToken);

    public async Task RevokeProjectAccessAsync(string tenantId, string appCode, string projectId, string connectionId, CancellationToken cancellationToken = default)
    {
        var group = SystemNotificationHub.BuildProjectManagementProjectGroupName(tenantId, appCode, projectId);
        await hubContext.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken);
        await hubContext.Clients.Client(connectionId).SendAsync("ProjectManagementAccessRevoked", new ProjectManagementProjectAccessRevokedEvent(projectId), cancellationToken);
    }
}
