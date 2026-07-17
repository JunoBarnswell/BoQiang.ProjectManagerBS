namespace AsterERP.Contracts.Platform;

public sealed record UserTenantMembershipUpsertRequest(
    string UserId,
    string TenantId,
    string? DeptId,
    string? PositionId,
    bool IsTenantAdmin,
    bool IsDefault,
    string Status,
    string? Remark);
