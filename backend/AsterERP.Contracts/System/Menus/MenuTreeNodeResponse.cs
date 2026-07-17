namespace AsterERP.Contracts.System.Menus;

public sealed record MenuTreeNodeResponse(
    string Id,
    string TenantId,
    string AppCode,
    string MenuName,
    string MenuCode,
    string? RoutePath,
    string? ComponentName,
    string? PageCode,
    string? ArtifactId,
    string? ScopeType,
    string? ConfigJson,
    string MenuType,
    int SortOrder,
    bool Visible,
    string? PermissionCode,
    string? Icon,
    IReadOnlyList<MenuTreeNodeResponse> Children);
