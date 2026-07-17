namespace AsterERP.Api.Infrastructure.Security;

public sealed record ResolvedAuthenticatedUser(
    string UserId,
    string UserName,
    string? TenantId,
    string? TenantName,
    string? AppCode,
    string? AppName,
    string? DeptId,
    string? PositionId,
    IReadOnlyList<string> RoleIds,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> PermissionCodes,
    string DataScope,
    bool IsAuthenticated,
    bool IsPlatformAdmin,
    bool IsTenantAdmin,
    string DisplayName = "",
    string? EmploymentId = null,
    string? EmploymentName = null,
    IReadOnlyList<string>? DeptIds = null,
    IReadOnlyList<string>? PositionIds = null);
