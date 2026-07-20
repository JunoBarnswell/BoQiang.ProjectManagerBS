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
            EventId: invalidation.EventId ?? Guid.NewGuid().ToString("N"),
            Sequence: invalidation.Version,
            ChangedFields: invalidation.ChangedFields ?? [invalidation.AggregateType, "updatedTime"],
            TraceId: invalidation.TraceId,
            TenantId: invalidation.TenantId,
            AppCode: invalidation.AppCode,
            AggregateVersion: invalidation.Version,
            WorkspaceSequence: invalidation.Version,
            ProjectSequence: invalidation.Version,
            Patch: invalidation.Patch,
            ClientMutationId: invalidation.ClientMutationId);
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
        return members
            .Append(owner)
            .Concat(invalidation.AdditionalHomeUserIds ?? [])
            .OfType<string>()
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public async Task RevokeProjectAccessAsync(string tenantId, string appCode, string projectId, string userId, long aggregateVersion = 0, CancellationToken cancellationToken = default)
    {
        var connections = subscriptions.GetConnectionIds(tenantId, appCode, projectId, userId);
        foreach (var connectionId in connections)
        {
            await realtimeTransport.RevokeProjectAccessAsync(tenantId, appCode, projectId, connectionId, cancellationToken);
            subscriptions.Unregister(connectionId, tenantId, appCode, projectId);
        }

        var version = aggregateVersion > 0 ? aggregateVersion : 1;
        var invalidation = new ProjectManagementRealtimeEvent(
            "Project",
            projectId,
            "project.access.revoked",
            version,
            projectId,
            EventId: Guid.NewGuid().ToString("N"),
            Sequence: version,
            ChangedFields: ["access"],
            TraceId: Guid.NewGuid().ToString("N"),
            TenantId: tenantId,
            AppCode: appCode,
            AggregateVersion: version,
            WorkspaceSequence: version,
            ProjectSequence: version,
            Patch: new Dictionary<string, object?> { ["isDeleted"] = true });
        await realtimeTransport.PublishHomeInvalidationAsync(tenantId, appCode, [userId], invalidation, cancellationToken);
    }
}
