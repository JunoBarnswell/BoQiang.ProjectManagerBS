using AsterERP.Contracts.Auth;
using AsterERP.Contracts.System.Menus;

namespace AsterERP.Contracts.Platform;

public sealed record ApplicationBackendEntryResponse(
    CurrentWorkspaceResponse CurrentWorkspace,
    CurrentUserResponse User,
    IReadOnlyList<MenuTreeNodeResponse> Menus,
    IReadOnlyList<string> PermissionCodes,
    BrandingResponse Branding,
    string? DefaultRoutePath = null);
