namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationSystemAssignmentResponse(
    string TenantAppId,
    string AppCode,
    string AppName,
    string? RunningVersion,
    string NoPermissionDisplay,
    IReadOnlyList<string> AuthorizedObjectIds,
    string? ConfigJson,
    string Status);
