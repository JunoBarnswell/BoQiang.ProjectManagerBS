namespace AsterERP.Api.Infrastructure.Messaging;

public sealed record MessageSendLogWriteRequest(
    string Channel,
    string Provider,
    string? Target,
    string TraceId,
    string? CorrelationId,
    bool Success,
    string? ErrorSummary,
    long DurationMs);
