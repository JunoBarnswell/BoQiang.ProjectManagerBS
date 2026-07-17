namespace AsterERP.Contracts.Platform;

public sealed record UserAppRoleResponse(
    string Id,
    string UserId,
    string UserName,
    string DisplayName,
    string TenantId,
    string TenantName,
    string AppCode,
    string AppName,
    string RoleId,
    string RoleName,
    bool IsDefault,
    string? Remark);
