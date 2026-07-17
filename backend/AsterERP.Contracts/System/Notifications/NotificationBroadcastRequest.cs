namespace AsterERP.Contracts.System.Notifications;

public sealed record NotificationBroadcastRequest(
    string EventName,
    string Message,
    string? Scope);
