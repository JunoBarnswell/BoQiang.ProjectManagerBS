using AsterERP.Contracts.Auth;
using AsterERP.Contracts.System.Menus;

namespace AsterERP.Api.Application.Auth;

public sealed record WorkspaceSessionSnapshot(
    CurrentWorkspaceResponse CurrentWorkspace,
    CurrentUserResponse User,
    IReadOnlyList<WorkspaceResponse> AvailableWorkspaces,
    IReadOnlyList<MenuTreeNodeResponse> Menus,
    IReadOnlyList<string> PermissionCodes,
    BrandingResponse Branding,
    string? DefaultRoutePath);
