namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleEntryTreeItemResponse(
    string Key,
    string Title,
    string Description,
    string Icon,
    string RoutePath,
    string PermissionCode,
    string VisitKind,
    string Accent,
    int? Count,
    string? CountLabel,
    string? RecentTargetTitle);
