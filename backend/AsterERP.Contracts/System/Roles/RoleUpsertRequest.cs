namespace AsterERP.Contracts.System.Roles;

public sealed record RoleUpsertRequest(
    string? TenantId,
    string? AppCode,
    string RoleName,
    string RoleCode,
    string DataScope,
    bool IsEnabled,
    string? Remark);
