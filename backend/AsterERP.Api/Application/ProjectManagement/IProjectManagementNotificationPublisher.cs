namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementNotificationPublisher
{
    Task PublishAsync(
        ProjectManagementNotification notification,
        CancellationToken cancellationToken = default);
}
