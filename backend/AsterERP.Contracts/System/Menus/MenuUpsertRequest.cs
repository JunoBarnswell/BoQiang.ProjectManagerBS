namespace AsterERP.Contracts.System.Menus;

public sealed record MenuUpsertRequest(
    string? TenantId,
    string? AppCode,
    string MenuName,
    string MenuCode,
    string? ParentCode,
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
