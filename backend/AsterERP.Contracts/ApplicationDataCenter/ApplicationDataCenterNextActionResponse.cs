namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterNextActionResponse(
    string ActionKey,
    string Title,
    string Description,
    string? RoutePath,
    string? PermissionCode);
