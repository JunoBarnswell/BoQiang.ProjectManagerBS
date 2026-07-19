namespace AsterERP.Api.Application.ProjectManagement;

using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

public sealed class ProjectManagementRealtimePublisher(
    IProjectManagementRealtimeTransport realtimeTransport,
    IProjectManagementRealtimeSubscriptionRegistry subscriptions,
    IWorkspaceDatabaseAccessor databaseAccessor) : IProjectManagementRealtimePublisher
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
            invalidation.TraceId,
            invalidation.TenantId,
            invalidation.AppCode,
            invalidation.Version,
            invalidation.Version,
            invalidation.Version);
        return PublishAsync(invalidation, projectId, payload, cancellationToken);
    }

    private async Task PublishAsync(ProjectManagementDataInvalidationEvent invalidation, string projectId, ProjectManagementRealtimeEvent payload, CancellationToken cancellationToken)
    {
        await realtimeTransport.PublishInvalidationAsync(invalidation.TenantId, invalidation.AppCode, projectId, payload, cancellationToken);
        var userIds = await GetVisibleHomeUsersAsync(invalidation, projectId, cancellationToken);
        await realtimeTransport.PublishHomeInvalidationAsync(invalidation.TenantId, invalidation.AppCode, userIds, payload, cancellationToken);
    }

    private async Task<IReadOnlyCollection<string>> GetVisibleHomeUsersAsync(ProjectManagementDataInvalidationEvent invalidation, string projectId, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var owners = await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == invalidation.TenantId && item.AppCode == invalidation.AppCode)
            .Select(item => item.OwnerUserId)
            .Take(1)
            .ToListAsync(cancellationToken);
        var owner = owners.FirstOrDefault();
        var members = await db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == invalidation.TenantId && item.AppCode == invalidation.AppCode && item.IsActive && !item.IsDeleted)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        return members.Append(owner).OfType<string>().Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray();
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
