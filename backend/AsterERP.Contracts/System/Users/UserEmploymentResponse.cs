namespace AsterERP.Contracts.System.Users;

public sealed record UserEmploymentResponse(
    string Id,
    string TenantId,
    string AppCode,
    string DeptId,
    string? DeptName,
    string PositionId,
    string? PositionName,
    string EmploymentName,
    bool IsPrimary,
    string Status,
    int SortOrder);
