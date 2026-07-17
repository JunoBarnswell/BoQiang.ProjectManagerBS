namespace AsterERP.Contracts.Logs;

public sealed record LoginLogListItemResponse(
    string Id,
    string TraceId,
    string UserName,
    string? UserId,
    string? UserDisplayName,
    string LoginResult,
    bool IsSuccess,
    string? FailureReason,
    string? ClientIp,
    string? UserAgent,
    DateTime CreatedTime);
