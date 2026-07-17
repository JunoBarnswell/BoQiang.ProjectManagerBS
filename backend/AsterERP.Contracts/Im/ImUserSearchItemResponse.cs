namespace AsterERP.Contracts.Im;

public sealed record ImUserSearchItemResponse(
    string UserId,
    string UserName,
    string DisplayName,
    string? DeptId,
    string? PositionId,
    string ImAccountId);
