namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationSystemAssignmentUpdateRequest(
    string AppCode,
    string? RunningVersion,
    string NoPermissionDisplay,
    IReadOnlyList<string> AuthorizedObjectIds);
