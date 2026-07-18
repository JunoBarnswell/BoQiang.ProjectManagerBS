using AsterERP.Api.Infrastructure.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementRealtimePublisher(
    IHubContext<SystemNotificationHub> hubContext,
    IProjectManagementRealtimeSubscriptionRegistry subscriptions) : IProjectManagementRealtimePublisher
{
    public Task PublishInvalidationAsync(ProjectManagementDataInvalidationEvent invalidation, CancellationToken cancellationToken = default)
    {
        var projectId = invalidation.ProjectId ?? invalidation.AggregateId;
        var payload = new ProjectManagementRealtimeEvent(
            invalidation.AggregateType,
            invalidation.AggregateId,
            invalidation.EventType,
            invalidation.Version,
            projectId);
        return hubContext.Clients.Group(SystemNotificationHub.BuildProjectManagementProjectGroupName(invalidation.TenantId, invalidation.AppCode, projectId)).SendAsync("ProjectManagementInvalidated", payload, cancellationToken);
    }

    public async Task RevokeProjectAccessAsync(string tenantId, string appCode, string projectId, string userId, CancellationToken cancellationToken = default)
    {
        var group = SystemNotificationHub.BuildProjectManagementProjectGroupName(tenantId, appCode, projectId);
        var connections = subscriptions.GetConnectionIds(tenantId, appCode, projectId, userId);
        foreach (var connectionId in connections)
        {
            await hubContext.Groups.RemoveFromGroupAsync(connectionId, group, cancellationToken);
            await hubContext.Clients.Client(connectionId).SendAsync("ProjectManagementAccessRevoked", new ProjectManagementProjectAccessRevokedEvent(projectId), cancellationToken);
            subscriptions.Unregister(connectionId, tenantId, appCode, projectId);
        }
    }
}
