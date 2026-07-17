using AsterERP.Contracts.System.Menus;

namespace AsterERP.Contracts.Auth;

public sealed record SwitchWorkspaceResponse(
    CurrentWorkspaceResponse CurrentWorkspace,
    CurrentUserResponse User,
    IReadOnlyList<MenuTreeNodeResponse> Menus,
    IReadOnlyList<string> PermissionCodes,
    BrandingResponse Branding,
    string? DefaultRoutePath = null);
