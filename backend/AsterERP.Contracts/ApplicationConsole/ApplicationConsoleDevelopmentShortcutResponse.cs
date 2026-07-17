namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleDevelopmentShortcutResponse(
    string Key,
    string Title,
    string Description,
    string Icon,
    string RoutePath,
    string PermissionCode,
    string VisitKind,
    string ActionText,
    string Accent,
    int? Count,
    string? CountLabel,
    string? RecentTargetTitle);
