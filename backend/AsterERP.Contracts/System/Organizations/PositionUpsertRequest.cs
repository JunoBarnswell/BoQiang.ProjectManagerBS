namespace AsterERP.Contracts.System.Organizations;

public sealed record PositionUpsertRequest(
    string PositionCode,
    string PositionName,
    string DeptId,
    string? PositionLevel,
    int SortOrder,
    string Status,
    string? Remark);
