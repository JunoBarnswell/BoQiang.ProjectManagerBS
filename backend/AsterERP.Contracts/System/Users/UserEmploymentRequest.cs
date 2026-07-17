namespace AsterERP.Contracts.System.Users;

public sealed record UserEmploymentRequest(
    string? Id,
    string? TenantId,
    string? AppCode,
    string DeptId,
    string PositionId,
    string? EmploymentName,
    bool IsPrimary,
    string Status,
    int SortOrder);
