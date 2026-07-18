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
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
