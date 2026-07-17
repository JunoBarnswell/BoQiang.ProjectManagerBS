namespace AsterERP.Contracts.System.Organizations;

public sealed record PositionListItemResponse(
    string Id,
    string PositionCode,
    string PositionName,
    string DeptId,
    string DeptName,
    string? PositionLevel,
    int SortOrder,
    string Status,
    int UserCount,
    string? Remark);
