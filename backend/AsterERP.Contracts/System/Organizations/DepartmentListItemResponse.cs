namespace AsterERP.Contracts.System.Organizations;

public sealed record DepartmentListItemResponse(
    string Id,
    string DeptCode,
    string DeptName,
    string? ParentId,
    string? ParentName,
    string? ManagerName,
    IReadOnlyList<string> LeaderUserIds,
    IReadOnlyList<string> LeaderNames,
    string? PhoneNumber,
    int SortOrder,
    string Status,
    int UserCount,
    int PositionCount,
    string? Remark);
