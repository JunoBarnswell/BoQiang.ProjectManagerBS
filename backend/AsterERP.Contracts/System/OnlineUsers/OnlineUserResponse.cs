namespace AsterERP.Contracts.System.OnlineUsers;

public sealed record OnlineUserResponse(
    string SessionId,
    string UserId,
    string UserName,
    string DisplayName,
    string? DeptId,
    string? ClientIp,
    string? UserAgent,
    DateTime ExpiresAt,
    DateTime? LastSeenTime,
    DateTime CreatedTime);
