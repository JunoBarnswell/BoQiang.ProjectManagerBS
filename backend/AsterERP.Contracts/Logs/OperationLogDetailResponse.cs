namespace AsterERP.Contracts.Logs;

public sealed record OperationLogDetailResponse(
    string Id,
    string TraceId,
    string? CorrelationId,
    string RequestPath,
    string RequestMethod,
    string? RouteDisplayName,
    string? ModuleName,
    string? OperationType,
    string? ActionName,
    string? RequestQuery,
    string? ClientIp,
    string? UserId,
    string? UserName,
    string? ErrorMessage,
    string? ExceptionSummary,
    int StatusCode,
    long DurationMs,
    bool IsSuccess,
    DateTime CreatedTime);
