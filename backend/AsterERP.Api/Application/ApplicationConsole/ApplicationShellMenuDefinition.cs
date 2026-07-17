namespace AsterERP.Api.Application.ApplicationConsole;

public sealed record ApplicationShellMenuDefinition(
    string MenuCode,
    string MenuName,
    string? ParentCode,
    string? RoutePath,
    string? ComponentName,
    string? PermissionCode,
    string Icon,
    int SortOrder,
    string MenuType = "Menu");
