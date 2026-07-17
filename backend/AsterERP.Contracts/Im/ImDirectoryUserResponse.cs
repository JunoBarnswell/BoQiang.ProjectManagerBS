namespace AsterERP.Contracts.Im;

public sealed record ImDirectoryUserResponse(
    string UserId,
    string UserName,
    string DisplayName,
    string DeptId,
    string? DeptName,
    string PositionId,
    string? PositionName,
    string EmploymentId,
    string EmploymentName,
    bool IsPrimaryEmployment,
    string ImAccountId,
    bool IsOnline,
    DateTime? LastSeenTime,
    string? ConversationId,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    int UnreadCount);
