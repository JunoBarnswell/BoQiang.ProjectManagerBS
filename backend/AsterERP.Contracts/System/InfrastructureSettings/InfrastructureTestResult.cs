namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record InfrastructureTestResult(
    bool Success,
    string Provider,
    string TraceId,
    string Message,
    long DurationMs);
