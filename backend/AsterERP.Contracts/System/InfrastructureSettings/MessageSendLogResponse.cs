namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record MessageSendLogResponse(
    string Id,
    string Channel,
    string Provider,
    string? MaskedTarget,
    string TraceId,
    string? CorrelationId,
    string Result,
    string? ErrorSummary,
    long DurationMs,
    DateTime CreatedTime);
