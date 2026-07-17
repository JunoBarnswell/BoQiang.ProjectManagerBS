using System.Text.Json;

namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed record ApplicationMonitoringEventRequest(
    string EventId,
    string EventType,
    string Source,
    string? PageId,
    string? RevisionId,
    string? ArtifactHash,
    bool Success,
    long? DurationMs,
    JsonElement Payload);
