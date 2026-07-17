using AsterERP.Contracts.System.Menus;

namespace AsterERP.Contracts.Auth;

public sealed record SessionResponse(
    CurrentUserResponse User,
    IReadOnlyList<WorkspaceResponse> AvailableWorkspaces,
    CurrentWorkspaceResponse? CurrentWorkspace,
    IReadOnlyList<MenuTreeNodeResponse> Menus,
    IReadOnlyList<string> PermissionCodes,
    BrandingResponse? Branding);
