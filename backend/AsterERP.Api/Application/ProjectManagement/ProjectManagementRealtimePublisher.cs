namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementRealtimePublisher(
    IProjectManagementRealtimeTransport realtimeTransport,
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
            projectId,
            invalidation.TraceId,
            invalidation.Version,
            [invalidation.AggregateType, "updatedTime"],
            invalidation.TraceId);
        return PublishAsync(invalidation, projectId, payload, cancellationToken);
    }

    private async Task PublishAsync(ProjectManagementDataInvalidationEvent invalidation, string projectId, ProjectManagementRealtimeEvent payload, CancellationToken cancellationToken)
    {
        await realtimeTransport.PublishInvalidationAsync(invalidation.TenantId, invalidation.AppCode, projectId, payload, cancellationToken);
        await realtimeTransport.PublishHomeInvalidationAsync(invalidation.TenantId, invalidation.AppCode, payload, cancellationToken);
    }

    public async Task RevokeProjectAccessAsync(string tenantId, string appCode, string projectId, string userId, CancellationToken cancellationToken = default)
    {
        var connections = subscriptions.GetConnectionIds(tenantId, appCode, projectId, userId);
        foreach (var connectionId in connections)
        {
            await realtimeTransport.RevokeProjectAccessAsync(tenantId, appCode, projectId, connectionId, cancellationToken);
            subscriptions.Unregister(connectionId, tenantId, appCode, projectId);
        }
    }
}
