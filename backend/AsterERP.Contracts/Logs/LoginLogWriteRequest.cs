namespace AsterERP.Contracts.Logs;

public sealed record LoginLogWriteRequest(
    string UserName,
    string? UserId,
    bool IsSuccess,
    string? FailureReason,
    string? ClientIp,
    string? UserAgent,
    string TraceId);
