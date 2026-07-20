namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementRealtimePublisher
{
    Task PublishInvalidationAsync(
        ProjectManagementDataInvalidationEvent invalidation,
        CancellationToken cancellationToken = default);

    Task RevokeProjectAccessAsync(
        string tenantId,
        string appCode,
        string projectId,
        string userId,
        long aggregateVersion = 0,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
