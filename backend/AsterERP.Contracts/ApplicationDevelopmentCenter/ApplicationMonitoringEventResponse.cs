namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed record ApplicationMonitoringEventResponse(
    string EventId,
    string EventType,
    string TraceId,
    bool Accepted,
    bool AlreadyAccepted);
