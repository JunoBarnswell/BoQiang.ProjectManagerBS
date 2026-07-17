namespace AsterERP.Contracts.System.Roles;

public sealed record RoleListItemResponse(
    string Id,
    string? TenantId,
    string? AppCode,
    string RoleName,
    string RoleCode,
    string DataScope,
    bool IsEnabled,
    int UserCount,
    int PermissionCount,
    string? Remark);
