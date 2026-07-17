namespace AsterERP.Contracts.System.Organizations;

public sealed record DepartmentUpsertRequest(
    string DeptCode,
    string DeptName,
    string? ParentId,
    string? ManagerName,
    IReadOnlyList<string>? LeaderUserIds,
    string? PhoneNumber,
    int SortOrder,
    string Status,
    string? Remark);
