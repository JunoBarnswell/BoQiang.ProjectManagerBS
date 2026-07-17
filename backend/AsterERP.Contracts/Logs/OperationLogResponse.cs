namespace AsterERP.Contracts.Logs;

public sealed record OperationLogResponse(
    string Id,
    string TraceId,
    string? CorrelationId,
    string RequestPath,
    string RequestMethod,
    string? RouteDisplayName,
    string? ModuleName,
    string? OperationType,
    string? ActionName,
    string? ClientIp,
    string? UserName,
    int StatusCode,
    long DurationMs,
    bool IsSuccess,
    DateTime CreatedTime);
