namespace AsterERP.Contracts.System.Users;

public sealed record UserListItemResponse(
    string Id,
    string UserName,
    string DisplayName,
    string? PhoneNumber,
    string? Email,
    string? DeptId,
    string? DeptName,
    string? PositionId,
    string? PositionName,
    bool IsAdmin,
    string Status,
    string DataScope,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> RoleNames,
    string? Remark,
    IReadOnlyList<UserEmploymentResponse>? Employments = null,
    string? PrimaryEmploymentId = null,
    string? PrimaryDeptId = null,
    string? PrimaryPositionId = null,
    string? EmploymentSummary = null);
