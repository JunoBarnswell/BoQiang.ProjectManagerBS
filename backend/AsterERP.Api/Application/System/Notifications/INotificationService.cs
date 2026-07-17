namespace AsterERP.Api.Application.System.Notifications;

public interface INotificationService
{
    Task BroadcastAsync(string eventName, string message, string? scope, CancellationToken cancellationToken = default);
}
