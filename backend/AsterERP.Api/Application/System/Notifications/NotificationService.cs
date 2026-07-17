using AsterERP.Api.Infrastructure.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace AsterERP.Api.Application.System.Notifications;

public sealed class NotificationService(IHubContext<SystemNotificationHub> hubContext) : INotificationService
{
    public Task BroadcastAsync(string eventName, string message, string? scope, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            EventName = eventName,
            Message = message,
            Scope = scope,
            SentAt = DateTime.UtcNow
        };

        return hubContext.Clients.All.SendAsync(eventName, payload, cancellationToken);
    }
}
