namespace AsterERP.Contracts.Im;

public sealed record ImDirectoryDepartmentNodeResponse(
    string DeptId,
    string DeptCode,
    string DeptName,
    string? ParentId,
    int SortOrder,
    IReadOnlyList<ImDirectoryUserResponse> Users,
    IReadOnlyList<ImDirectoryDepartmentNodeResponse> Children);
