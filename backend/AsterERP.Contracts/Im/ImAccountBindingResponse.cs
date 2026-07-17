namespace AsterERP.Contracts.Im;

public sealed record ImAccountBindingResponse(
    string TenantId,
    string UserId,
    string ImAccountId,
    string DisplayName,
    string Status,
    DateTime BoundAt);
