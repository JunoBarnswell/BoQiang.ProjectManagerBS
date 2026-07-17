namespace AsterERP.Contracts.Platform;

public sealed record UserTenantMembershipResponse(
    string Id,
    string UserId,
    string UserName,
    string DisplayName,
    string TenantId,
    string TenantName,
    string? DeptId,
    string? DeptName,
    string? PositionId,
    string? PositionName,
    bool IsTenantAdmin,
    bool IsDefault,
    string Status,
    string? Remark);
