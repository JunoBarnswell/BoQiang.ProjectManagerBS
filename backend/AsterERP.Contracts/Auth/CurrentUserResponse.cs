using AsterERP.Contracts.System.Users;

namespace AsterERP.Contracts.Auth;

public sealed record CurrentUserResponse(
    string UserId,
    string UserName,
    string DisplayName,
    string? TenantId,
    string? TenantName,
    string? AppCode,
    string? AppName,
    string? DeptId,
    string? PositionId,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> PermissionCodes,
    string DataScope,
    bool IsAdmin,
    bool IsPlatformAdmin,
    bool IsTenantAdmin,
    string? EmploymentId = null,
    string? EmploymentName = null,
    IReadOnlyList<string>? DeptIds = null,
    IReadOnlyList<string>? PositionIds = null,
    IReadOnlyList<UserEmploymentResponse>? Employments = null);
