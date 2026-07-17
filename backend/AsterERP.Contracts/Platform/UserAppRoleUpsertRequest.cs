namespace AsterERP.Contracts.Platform;

public sealed record UserAppRoleUpsertRequest(
    string UserId,
    string TenantId,
    string AppCode,
    string RoleId,
    bool IsDefault,
    string? Remark);
