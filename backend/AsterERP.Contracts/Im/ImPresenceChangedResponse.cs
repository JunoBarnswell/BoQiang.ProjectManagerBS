namespace AsterERP.Contracts.Im;

public sealed record ImPresenceChangedResponse(
    string UserId,
    string TenantId,
    string AppCode,
    bool IsOnline,
    DateTime? LastSeenTime);
