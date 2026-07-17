namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterActionResultResponse(
    bool Success,
    string Status,
    string Message,
    long DurationMs,
    string? DetailJson,
    IReadOnlyList<ApplicationDataCenterNextActionResponse> NextActions);
