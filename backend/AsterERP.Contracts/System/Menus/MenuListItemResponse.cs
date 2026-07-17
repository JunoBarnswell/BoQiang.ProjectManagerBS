namespace AsterERP.Contracts.System.Menus;

public sealed record MenuListItemResponse(
    string Id,
    string TenantId,
    string AppCode,
    string MenuName,
    string MenuCode,
    string? ParentCode,
    string? ParentMenuName,
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
    string? Remark);
