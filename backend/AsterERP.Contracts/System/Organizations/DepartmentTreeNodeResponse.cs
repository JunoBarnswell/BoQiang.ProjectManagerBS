namespace AsterERP.Contracts.System.Organizations;

public sealed record DepartmentTreeNodeResponse(
    string Id,
    string DeptCode,
    string DeptName,
    string? ParentId,
    IReadOnlyList<string> LeaderUserIds,
    IReadOnlyList<string> LeaderNames,
    int SortOrder,
    string Status,
    IReadOnlyList<DepartmentTreeNodeResponse> Children);
