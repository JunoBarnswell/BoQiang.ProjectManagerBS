namespace AsterERP.Contracts.Im;

public sealed record ImMessagePageResponse(
    IReadOnlyList<ImMessageResponse> Items,
    string? NextCursor,
    bool HasMore);
