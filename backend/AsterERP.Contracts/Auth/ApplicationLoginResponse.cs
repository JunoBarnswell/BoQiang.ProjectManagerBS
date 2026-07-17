using AsterERP.Contracts.System.Menus;

namespace AsterERP.Contracts.Auth;

public sealed record ApplicationLoginResponse(
    string AccessToken,
    CurrentUserResponse User,
    CurrentWorkspaceResponse CurrentWorkspace,
    IReadOnlyList<MenuTreeNodeResponse> Menus,
    IReadOnlyList<string> PermissionCodes,
    BrandingResponse Branding,
    string? DefaultRoutePath = null);
