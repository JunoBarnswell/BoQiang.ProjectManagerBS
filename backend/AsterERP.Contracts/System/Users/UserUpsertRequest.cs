namespace AsterERP.Contracts.System.Users;

public sealed record UserUpsertRequest(
    string UserName,
    string DisplayName,
    string Password,
    string? PhoneNumber,
    string? Email,
    string? DeptId,
    string? PositionId,
    bool IsAdmin,
    string Status,
    IReadOnlyList<string> RoleIds,
    string? Remark,
    IReadOnlyList<UserEmploymentRequest>? Employments = null);
